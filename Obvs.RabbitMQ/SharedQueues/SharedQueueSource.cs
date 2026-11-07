using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Obvs.Hosting;
using Obvs.Serialization;
using Obvs.Transports;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Obvs.RabbitMQ.SharedQueues
{
    // Receives from a queue the service's buses share, so each message goes to one of them. Messages stay on the queue
    // until they're settled, and the channel's prefetch count caps how many this bus holds at once.
    internal sealed class SharedQueueSource<TMessage> : IMessageSource<TMessage> where TMessage : class
    {
        private static readonly TimeSpan FirstRetryDelay = TimeSpan.FromMilliseconds(500);
        private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(30);

        private readonly Lazy<IConnection> _connection;
        private readonly SharedQueueTopology _topology;
        private readonly string _queue;
        private readonly IDictionary<string, IMessageDeserializer<TMessage>> _deserializers;
        private readonly ushort _prefetchCount;
        private readonly ILogger _logger;
        private readonly object _gate = new object();
        private TimeSpan _retryDelay;

        public SharedQueueSource(Lazy<IConnection> connection, SharedQueueTopology topology, string queue,
            IEnumerable<IMessageDeserializer<TMessage>> deserializers, int maxConcurrentMessages, ILogger logger)
        {
            _connection = connection;
            _topology = topology;
            _queue = queue;
            _deserializers = deserializers.ToDictionary(deserializer => deserializer.TypeName);
            _prefetchCount = (ushort)Math.Min(maxConcurrentMessages, ushort.MaxValue);
            _logger = logger;

            // Setup blocks so that the queue is bound by the time Subscribe returns, as callers commonly send straight after
            // subscribing. RabbitMQ.Client doesn't capture the synchronization context, so blocking here can't deadlock.
            Messages = Observable.Create<TMessage>(observer => SubscribeAsync(observer).GetAwaiter().GetResult());
        }

        public IObservable<TMessage> Messages { get; }

        private async Task<IDisposable> SubscribeAsync(IObserver<TMessage> observer)
        {
            IChannel channel = null;
            try
            {
                channel = await _connection.Value.CreateChannelAsync().ConfigureAwait(false);
                await _topology.DeclareAsync(channel, _queue).ConfigureAwait(false);
                await channel.BasicQosAsync(0, _prefetchCount, false).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                channel?.Dispose();
                return ReportLater(observer, exception);
            }
            ResetRetryDelay();

            // Hand messages to subscribers on the task pool, in order. Subscriber code running on RabbitMQ's consumer-dispatch
            // thread could otherwise deadlock it, for example by disposing the bus, which waits for that thread.
            var received = new Subject<TMessage>();
            var subscription = received.ObserveOn(TaskPoolScheduler.Default).Subscribe(observer);
            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (sender, delivery) =>
            {
                var message = await ReceiveAsync(channel, delivery).ConfigureAwait(false);
                if (message != null)
                {
                    received.OnNext(message);
                }
            };
            var consumerTag = await channel.BasicConsumeAsync(_queue, false, consumer).ConfigureAwait(false);

            // Closing the channel gives the messages it hasn't settled back to the queue
            return Disposable.Create(() =>
            {
                subscription.Dispose();
                if (channel.IsOpen)
                {
                    if (consumer.IsRunning)
                    {
                        channel.BasicCancelAsync(consumerTag).GetAwaiter().GetResult();
                    }
                    channel.CloseAsync().GetAwaiter().GetResult();
                }
                channel.Dispose();
            });
        }

        // Messages of a type this service doesn't know are removed. Ones it can't read are delivered again, as another of the
        // service's buses may be able to during a rolling deployment, then dead-lettered.
        private async Task<TMessage> ReceiveAsync(IChannel channel, BasicDeliverEventArgs delivery)
        {
            var body = delivery.Body.ToArray();
            var settlement = new MessageSettlement(channel, delivery, body, _topology);
            var typeName = delivery.RoutingKey.Substring(delivery.RoutingKey.LastIndexOf('.') + 1);
            if (!_deserializers.TryGetValue(typeName, out var deserializer))
            {
                _logger.LogWarning("RabbitMQ consumer of {Queue} skipped a {MessageType}, which it has no deserializer for", _queue, typeName);
                await SettleAsync(settlement.CompleteAsync()).ConfigureAwait(false);
                return null;
            }

            TMessage message;
            try
            {
                message = deserializer.Deserialize(new MemoryStream(body));
            }
            catch (Exception exception)
            {
                ObvsTransportTelemetry.RecordUndeserializable(_topology.ServiceName, typeName, exception);
                _logger.LogWarning(exception, "RabbitMQ consumer of {Queue} couldn't read a {MessageType}, delivered {DeliveryCount} times",
                    _queue, typeName, settlement.DeliveryCount);
                await SettleAsync(settlement.AbandonAsync(exception, MessageSettlement.Undeserializable)).ConfigureAwait(false);
                return null;
            }
            ObvsReceivedMessage.Attach(message, Headers(delivery.BasicProperties.Headers), settlement);
            return message;
        }

        private async Task SettleAsync(Task settling)
        {
            try
            {
                await settling.ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                // The channel closed: the broker delivers the message again
                _logger.LogWarning(exception, "RabbitMQ consumer of {Queue} couldn't settle a message", _queue);
            }
        }

        // The headers the broker adds, which aren't the app's
        private static readonly HashSet<string> BrokerHeaders = new HashSet<string>(StringComparer.Ordinal)
        {
            MessageSettlement.BrokerDeliveryCountHeader, "x-acquired-count", "x-death", "x-first-death-exchange", "x-first-death-queue",
            "x-first-death-reason", "x-last-death-exchange", "x-last-death-queue", "x-last-death-reason",
        };

        // AMQP string headers arrive as UTF-8 bytes
        private static IReadOnlyDictionary<string, string> Headers(IDictionary<string, object> amqpHeaders)
        {
            var headers = new Dictionary<string, string>();
            if (amqpHeaders == null)
            {
                return headers;
            }
            foreach (var header in amqpHeaders)
            {
                if (BrokerHeaders.Contains(header.Key))
                {
                    continue;
                }
                var value = header.Value is byte[] bytes ? Encoding.UTF8.GetString(bytes) : header.Value?.ToString();
                if (value != null)
                {
                    headers[header.Key] = value;
                }
            }
            return headers;
        }

        // The bus subscribes again as soon as a source reports an error, so wait longer before reporting each one in a row
        private IDisposable ReportLater(IObserver<TMessage> observer, Exception exception)
        {
            var delay = NextRetryDelay();
            if (delay == FirstRetryDelay)
            {
                _logger.LogWarning(exception, "RabbitMQ can't receive from queue {Queue}, and retries until it can: {Message}", _queue, exception.Message);
            }
            var cancellation = new CancellationTokenSource();
            Task.Delay(delay, cancellation.Token).ContinueWith(waited =>
            {
                if (!waited.IsCanceled)
                {
                    observer.OnError(exception);
                }
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            return Disposable.Create(cancellation.Cancel);
        }

        private TimeSpan NextRetryDelay()
        {
            lock (_gate)
            {
                var doubled = TimeSpan.FromTicks(_retryDelay.Ticks * 2);
                return _retryDelay = _retryDelay == TimeSpan.Zero ? FirstRetryDelay : doubled < MaxRetryDelay ? doubled : MaxRetryDelay;
            }
        }

        private void ResetRetryDelay()
        {
            lock (_gate)
            {
                _retryDelay = TimeSpan.Zero;
            }
        }

        public void Dispose()
        {
        }
    }
}
