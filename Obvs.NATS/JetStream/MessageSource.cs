using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using NATS.Client.JetStream;
using Obvs.Serialization;

namespace Obvs.NATS.JetStream
{
    internal sealed class MessageSource<TMessage> : IMessageSource<TMessage>
        where TMessage : class
    {
        // How long Dispose waits for messages being delivered to finish settling
        private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(10);

        private readonly IDictionary<string, IMessageDeserializer<TMessage>> _deserializers;
        private readonly INatsJSContext _jetStream;
        private readonly JetStreamTopology.Destination _destination;
        private readonly string _serviceName;
        private readonly NatsJetStreamOptions _options;
        private readonly ConcurrentDictionary<Receiver, bool> _receivers = new ConcurrentDictionary<Receiver, bool>();

        public MessageSource(INatsJSContext jetStream, JetStreamTopology.Destination destination, string serviceName,
            IEnumerable<IMessageDeserializer<TMessage>> deserializers, NatsJetStreamOptions options)
        {
            _jetStream = jetStream;
            _destination = destination;
            _serviceName = serviceName;
            _options = options;
            _deserializers = deserializers.ToDictionary(deserializer => deserializer.GetTypeName());

            Messages = Observable.Create<TMessage>(observer =>
                {
                    var receiver = new Receiver(this, observer);
                    _receivers[receiver] = true;
                    return Disposable.Create(() =>
                    {
                        _receivers.TryRemove(receiver, out _);
                        receiver.Stop();
                    });
                })
                // Hand messages to subscribers on the task pool, in order. Subscriber code running on the receive loop
                // could otherwise deadlock it, for example by disposing the bus, which waits for that loop. With
                // MaxConcurrentMessages above 1, messages arrive at the same time, but observers take one at a time.
                .Synchronize()
                .ObserveOn(TaskPoolScheduler.Default)
                // One receive loop per consumer, however many subscribe, such as each pending request waiting for its
                // response: loops of their own would take each other's messages
                .Publish()
                .RefCount();
        }

        public IObservable<TMessage> Messages { get; }

        // Messages of a type this service doesn't know are skipped
        private TMessage Deserialize(INatsJSMsg<byte[]> received)
        {
            var typeName = received.Subject.Substring(received.Subject.LastIndexOf('.') + 1);
            if (!_deserializers.TryGetValue(typeName, out var deserializer))
            {
                return null;
            }

            var message = deserializer.Deserialize(new MemoryStream(received.Data ?? new byte[0]));
            if (received.Headers != null)
            {
                var headers = new Dictionary<string, string>();
                foreach (var header in received.Headers)
                {
                    if (header.Key == MessagePublisher<TMessage>.ReplyToHeader)
                    {
                        ReplyAddresses.Set(message, header.Value.ToString());
                    }
                    else
                    {
                        headers[header.Key] = header.Value.ToString();
                    }
                }
                if (headers.Count > 0)
                {
                    ObvsMessageHeaders.Set(message, headers);
                }
            }
            return message;
        }

        // The endpoint closes its connection next, so stop receiving first, even for subscribers still subscribed
        public void Dispose()
        {
            foreach (var receiver in _receivers.Keys)
            {
                receiver.Stop();
            }
        }

        // Pulls one message at a time for each free slot, so a bus takes only what it's ready to handle, and the service's
        // other buses get the rest. A slot is freed once its message is settled.
        private sealed class Receiver
        {
            private readonly MessageSource<TMessage> _source;
            private readonly IObserver<TMessage> _observer;
            private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();
            private readonly SemaphoreSlim _slots;
            private readonly ConcurrentDictionary<MessageSettlement, bool> _waiting = new ConcurrentDictionary<MessageSettlement, bool>();
            private readonly ConcurrentDictionary<Task, bool> _delivering = new ConcurrentDictionary<Task, bool>();
            private readonly Task _receiving;
            private volatile bool _stopped;
            private int _stopping;

            public Receiver(MessageSource<TMessage> source, IObserver<TMessage> observer)
            {
                _source = source;
                _observer = observer;
                _slots = new SemaphoreSlim(source._options.MaxConcurrentMessages);
                _receiving = Task.Run(ReceiveAsync);
            }

            // Messages still waiting for a handler stop waiting, so stopping can't wait on a handler that is itself
            // stopping the bus
            public void Stop()
            {
                if (Interlocked.Exchange(ref _stopping, 1) == 1)
                {
                    return;
                }
                _stopped = true;
                _cancellation.Cancel();
                foreach (var settlement in _waiting.Keys)
                {
                    settlement.StopWaiting();
                }
                try
                {
#pragma warning disable VSTHRD002 // IDisposable.Dispose is synchronous
                    Task.WhenAll(_delivering.Keys.Concat(new[] { _receiving })).Wait(StopTimeout);
#pragma warning restore VSTHRD002
                }
                catch (AggregateException)
                {
                }
            }

            private async Task ReceiveAsync()
            {
                var token = _cancellation.Token;
                try
                {
                    var consumer = await _source._jetStream.GetConsumerAsync(_source._destination.Stream, _source._destination.Consumer, token).ConfigureAwait(false);
                    while (!token.IsCancellationRequested)
                    {
                        await _slots.WaitAsync(token).ConfigureAwait(false);
                        INatsJSMsg<byte[]> received;
                        try
                        {
                            received = await consumer.NextAsync<byte[]>(cancellationToken: token).ConfigureAwait(false);
                        }
                        catch
                        {
                            _slots.Release();
                            throw;
                        }

                        if (received == null)
                        {
                            _slots.Release();
                            continue;
                        }

                        var delivering = DeliverAsync(received);
                        _delivering[delivering] = true;
                        _ = delivering.ContinueWith(task =>
                        {
                            _delivering.TryRemove(task, out _);
                            _slots.Release();
                        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                }
                catch (Exception exception)
                {
                    if (!_stopped)
                    {
                        _observer.OnError(exception);
                    }
                }
            }

            private async Task DeliverAsync(INatsJSMsg<byte[]> received)
            {
                var settlement = new MessageSettlement(received, _source._jetStream, _source._serviceName, _source._options);
                TMessage message;
                try
                {
                    message = _source.Deserialize(received);
                }
                catch (Exception exception)
                {
                    // JetStream delivers it again, and it's dead-lettered after MaxDeliveryAttempts
                    await IgnoreFailureAsync(() => settlement.AbandonAsync(exception)).ConfigureAwait(false);
                    return;
                }
                if (message == null)
                {
                    await IgnoreFailureAsync(settlement.CompleteAsync).ConfigureAwait(false);
                    return;
                }

                ObvsMessageSettlement.Set(message, settlement);
                _waiting[settlement] = true;
                try
                {
                    _observer.OnNext(message);
                    if (_stopped)
                    {
                        settlement.StopWaiting();
                    }
                    await settlement.WaitAsync().ConfigureAwait(false);
                }
                finally
                {
                    _waiting.TryRemove(settlement, out _);
                }
            }

            // JetStream delivers a message again if settling it fails
            private static async Task IgnoreFailureAsync(Func<Task> settle)
            {
                try
                {
                    await settle().ConfigureAwait(false);
                }
                catch (Exception)
                {
                }
            }
        }
    }
}
