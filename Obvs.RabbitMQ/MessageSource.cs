using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Obvs.RabbitMQ.Extensions;
using Obvs.Serialization;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

// v6's RabbitMQ transport building blocks, which UseRabbitMq(...) is built on: its public members are exempt from the rule that every one is documented
#pragma warning disable CS1570, CS1572, CS1591

namespace Obvs.RabbitMQ
{
    public class MessageSource<TMessage> : IMessageSource<TMessage> where TMessage : class
    {
        private readonly Lazy<IConnection> _connection;
        private readonly string _exchange;
        private readonly string _queueNamePrefix;
        private readonly string _uniqueQueueSuffix;
        private readonly IDictionary<string, IMessageDeserializer<TMessage>> _deserializers;
        private readonly string _routingKey;
        private int _subscriberCount;

        /// <summary>
        /// Creates a new MessageSource
        /// </summary>
        /// <param name="connection">Lazy connection to RabbitMQ which will connect on first subscription</param>
        /// <param name="deserializers">Collection of deserializers, one per message type</param>
        /// <param name="exchange">RabbitMQ Exchange name</param>
        /// <param name="routingKeyPrefix">Any routing key prefix for filtering messages</param>
        /// <param name="uniqueQueueSuffix">Only required if you intend to create two or more message sources to subscribe to the same source in the same process</param>
        public MessageSource(Lazy<IConnection> connection, IEnumerable<IMessageDeserializer<TMessage>> deserializers, 
            string exchange, string routingKeyPrefix, string uniqueQueueSuffix = "")
        {
            _connection = connection;
            _exchange = exchange;
            _uniqueQueueSuffix = uniqueQueueSuffix;
            _deserializers = deserializers.ToDictionary(d => d.GetTypeName());
            _routingKey = string.Format("{0}.*", routingKeyPrefix);
            _queueNamePrefix = routingKeyPrefix.StartsWith(exchange) ? routingKeyPrefix : string.Format("{0}.{1}*", _exchange, routingKeyPrefix);
        }

        public void Dispose()
        {
        }

        public IObservable<TMessage> Messages
        {
            get
            {
                // Setup blocks so that the queue is bound by the time Subscribe returns, as callers
                // commonly publish straight after subscribing. RabbitMQ.Client doesn't capture the
                // synchronization context, so blocking here can't deadlock.
                return Observable.Create<TMessage>(observer => SubscribeAsync(observer).GetAwaiter().GetResult());
            }
        }

        private async Task<IDisposable> SubscribeAsync(IObserver<TMessage> observer)
        {
            var channel = await _connection.Value.CreateChannelAsync().ConfigureAwait(false);
            await channel.ExchangeDeclareAsync(_exchange, ExchangeType.Topic).ConfigureAwait(false);

            var queueName = GetQueueName();
            var queue = await channel.QueueDeclareAsync(queueName).ConfigureAwait(false);
            await channel.QueueBindAsync(queue.QueueName, _exchange, _routingKey).ConfigureAwait(false);

            var consumer = new AsyncEventingBasicConsumer(channel);

            // Deserialize on RabbitMQ's consumer-dispatch thread, while the body's buffer is valid, then hand
            // messages to subscribers on the task pool, in order. Subscriber code running on the dispatch thread
            // could otherwise deadlock it, for example by disposing the bus, which waits for that thread.
            var subscription = consumer.ToObservable()
                                       .Select(Deserialize)
                                       .ObserveOn(TaskPoolScheduler.Default)
                                       .Subscribe(observer);

            var consumerTag = await channel.BasicConsumeAsync(queue.QueueName, true, consumer).ConfigureAwait(false);

            return Disposable.Create(() =>
            {
                subscription.Dispose();
                if (channel.IsOpen)
                {
                    if (consumer.IsRunning)
                    {
                        channel.BasicCancelAsync(consumerTag).GetAwaiter().GetResult();
                    }
                    channel.QueueUnbindAsync(queue.QueueName, _exchange, _routingKey).GetAwaiter().GetResult();
                    channel.QueueDeleteAsync(queue.QueueName).GetAwaiter().GetResult();
                    channel.CloseAsync().GetAwaiter().GetResult();
                }
                channel.Dispose();
            });
        }

        private string GetQueueName()
        {
            // thread-safe increment of the subscriber count
            Interlocked.Increment(ref _subscriberCount);

            // ensure queue name is readable, but also unique to the process and subscriber
            // as we are using an exclusive queue per consumer/subscription
            var queueName = string.Format("{0}-{1}-{2}-{3}-{4}", 
                _queueNamePrefix, 
                Environment.MachineName,
                Environment.UserName, 
                Process.GetCurrentProcess().Id,
                _subscriberCount);

            if (!string.IsNullOrEmpty(_uniqueQueueSuffix))
            {
                queueName = string.Format("{0}-{1}", queueName, _uniqueQueueSuffix);
            }

            return queueName;
        }

        private TMessage Deserialize(BasicDeliverEventArgs deliverEventArgs)
        {
            var deserializer = GetDeserializer(deliverEventArgs);
            // The body's buffer is only valid until the delivery handler returns, so copy it
            var message = deserializer.Deserialize(deliverEventArgs.Body.ToArray());
            SetHeaders(message, deliverEventArgs.BasicProperties.Headers);
            return message;
        }

        // AMQP string headers arrive as UTF-8 bytes
        private static void SetHeaders(TMessage message, IDictionary<string, object> amqpHeaders)
        {
            if (amqpHeaders == null || amqpHeaders.Count == 0)
            {
                return;
            }

            var headers = new Dictionary<string, string>();
            foreach (var header in amqpHeaders)
            {
                var value = header.Value is byte[] bytes ? Encoding.UTF8.GetString(bytes) : header.Value?.ToString();
                if (value != null)
                {
                    headers[header.Key] = value;
                }
            }
            ObvsMessageHeaders.Set(message, headers);
        }

        private IMessageDeserializer<TMessage> GetDeserializer(BasicDeliverEventArgs deliverEventArgs)
        {
            var typeName = deliverEventArgs.RoutingKey.Split('.').LastOrDefault();

            if (typeName == null)
            {
                throw new Exception(string.Format("Unable to parse typeName from RoutingKey '{0}'", deliverEventArgs.RoutingKey));
            }

            IMessageDeserializer<TMessage> deserializer;
            if (_deserializers.TryGetValue(typeName, out deserializer))
            {
                return deserializer;
            }

            if (_deserializers.Count == 1)
            {
                return _deserializers.Single().Value;
            }

            throw new Exception(string.Format("Unable to find deserializer for typeName '{0}'", typeName));
        }
    }
}
