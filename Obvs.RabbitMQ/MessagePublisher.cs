using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Obvs.Serialization;
using RabbitMQ.Client;

// v6's RabbitMQ transport building blocks, public for v6's fluent configuration; v7 apps use UseRabbitMq(...): its public members are exempt from the rule that every one is documented
#pragma warning disable CS1570, CS1572, CS1591

namespace Obvs.RabbitMQ
{
    public class MessagePublisher<TMessage> : IMessagePublisher<TMessage> where TMessage : class
    {
        private readonly IMessageSerializer _serializer;
        private readonly string _exchange;
        private readonly string _routingKeyPrefix;
        private Lazy<Task<IChannel>> _channel;

        public MessagePublisher(Lazy<IConnection> connection, IMessageSerializer serializer, string exchange,
            string routingKeyPrefix)
        {
            _serializer = serializer;
            _exchange = exchange;
            _routingKeyPrefix = routingKeyPrefix;

            _channel = new Lazy<Task<IChannel>>(async () =>
            {
                var channel = await connection.Value.CreateChannelAsync().ConfigureAwait(false);
                await channel.ExchangeDeclareAsync(exchange, ExchangeType.Topic).ConfigureAwait(false);
                return channel;
            }, LazyThreadSafetyMode.ExecutionAndPublication);
        }

        public async Task PublishAsync(TMessage message, CancellationToken cancellationToken = default)
        {
            var body = _serializer.Serialize(message);
            var properties = new BasicProperties {ContentType = "bytes", Headers = GetHeaders(message)};
            var routingKey = GetRoutingKey(message);
            var channel = await _channel.Value.ConfigureAwait(false);
            await channel.BasicPublishAsync(_exchange, routingKey, false, properties, body, cancellationToken).ConfigureAwait(false);
        }

        // The message's Obvs headers (such as trace context) as AMQP headers
        private static IDictionary<string, object> GetHeaders(TMessage message)
        {
            var headers = ObvsMessageHeaders.Get(message);
            return headers.Count == 0 ? null : headers.ToDictionary(header => header.Key, header => (object)header.Value);
        }

        private string GetRoutingKey(TMessage message)
        {
            return string.Format("{0}.{1}", _routingKeyPrefix, message.GetType().Name);
        }

        public void Dispose()
        {
            if (_channel.IsValueCreated && _channel.Value.Status == TaskStatus.RanToCompletion)
            {
                var channel = _channel.Value.Result;
                channel.CloseAsync().GetAwaiter().GetResult();
                channel.Dispose();
                _channel = null;
            }
        }
    }
}
