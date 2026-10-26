using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Obvs.Hosting;
using Obvs.MessageProperties;
using Obvs.Serialization;

namespace Obvs.AzureSB
{
    internal sealed class MessageSource<TMessage> : IMessageSource<TMessage>
        where TMessage : class
    {
        private readonly IDictionary<string, IMessageDeserializer<TMessage>> _deserializers;

        /// <param name="createProcessor">Creates a processor for the queue or subscription, once per subscription to <see cref="Messages"/></param>
        /// <param name="options">How many messages to deliver at once, how long to renew their locks, and how many times to deliver a failing one</param>
        public MessageSource(Func<ServiceBusProcessorOptions, ServiceBusProcessor> createProcessor, IEnumerable<IMessageDeserializer<TMessage>> deserializers, AzureServiceBusOptions options)
        {
            _deserializers = deserializers.ToDictionary(deserializer => deserializer.GetTypeName());

            Messages = Observable.Create<TMessage>(observer =>
                {
                    // One message at a time (MaxConcurrentCalls 1, the default) keeps them in order. Each waits in its callback
                    // until it's settled, by its handler or as it arrives if no handler takes it, while its lock is renewed.
                    var processor = createProcessor(new ServiceBusProcessorOptions
                    {
                        AutoCompleteMessages = false,
                        MaxConcurrentCalls = options.MaxConcurrentCalls,
                        MaxAutoLockRenewalDuration = options.MaxLockRenewalDuration,
                    });
                    var waiting = new ConcurrentDictionary<MessageSettlement, bool>();
                    var stopped = false;
                    processor.ProcessMessageAsync += async args =>
                    {
                        TMessage message;
                        try
                        {
                            message = Deserialize(args.Message);
                        }
                        catch (Exception)
                        {
                            // Service Bus redelivers it, then dead-letters it after the entity's MaxDeliveryCount
                            await args.AbandonMessageAsync(args.Message).ConfigureAwait(false);
                            return;
                        }
                        if (message == null)
                        {
                            await args.CompleteMessageAsync(args.Message).ConfigureAwait(false);
                            return;
                        }

                        var settlement = new MessageSettlement(args, options.MaxDeliveryAttempts);
                        ObvsMessageSettlement.Set(message, settlement);
                        waiting[settlement] = true;
                        try
                        {
                            observer.OnNext(message);
                            if (stopped)
                            {
                                settlement.StopWaiting();
                            }
                            await settlement.Settled.ConfigureAwait(false);
                        }
                        finally
                        {
                            waiting.TryRemove(settlement, out _);
                        }
                    };
                    processor.ProcessErrorAsync += args =>
                    {
                        // The processor retries transient errors itself
                        if (!stopped && args.ErrorSource != ServiceBusErrorSource.ProcessMessageCallback &&
                            !(args.Exception is ServiceBusException serviceBusException && serviceBusException.IsTransient))
                        {
                            observer.OnError(args.Exception);
                        }
                        return Task.CompletedTask;
                    };
                    processor.StartProcessingAsync().GetAwaiter().GetResult();

                    // Callbacks still waiting for a handler stop waiting, so stopping the processor can't wait on a
                    // handler that is itself stopping the bus
                    return Disposable.Create(() =>
                    {
                        stopped = true;
                        foreach (var settlement in waiting.Keys)
                        {
                            settlement.StopWaiting();
                        }
                        processor.DisposeAsync().AsTask().GetAwaiter().GetResult();
                    });
                })
                // Hand messages to subscribers on the task pool, in order. Subscriber code running in the processor's
                // callback could otherwise deadlock it, for example by disposing the bus, which waits for that callback.
                // With MaxConcurrentCalls above 1, callbacks deliver at the same time, but observers take one at a time
                .Synchronize()
                .ObserveOn(TaskPoolScheduler.Default);
        }

        public IObservable<TMessage> Messages { get; }

        // Messages of a type this service doesn't know are skipped
        private TMessage Deserialize(ServiceBusReceivedMessage received)
        {
            if (!(received.ApplicationProperties.TryGetValue(MessagePropertyNames.TypeName, out var typeName) && typeName is string name) ||
                !_deserializers.TryGetValue(name, out var deserializer))
            {
                return null;
            }

            var message = deserializer.Deserialize(received.Body.ToStream());
            var headers = received.ApplicationProperties
                .Where(property => property.Key != MessagePropertyNames.TypeName && property.Value is string)
                .ToDictionary(property => property.Key, property => (string)property.Value);
            if (headers.Count > 0)
            {
                ObvsMessageHeaders.Set(message, headers);
            }
            if (!string.IsNullOrEmpty(received.ReplyTo))
            {
                ReplyAddresses.Set(message, received.ReplyTo);
            }
            return message;
        }

        public void Dispose()
        {
        }
    }
}
