using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Obvs.Serialization;
using Obvs.Transports;
using RabbitMQ.Client;

namespace Obvs.RabbitMQ
{
    /// <summary>
    /// Publishes messages to a RabbitMQ topic exchange, as v6 did, for an app that builds its own endpoints. Each is
    /// published with the routing key <c>{routingKeyPrefix}.{type's name}</c>, and its headers as AMQP headers.
    /// </summary>
    /// <typeparam name="TMessage">The type of the messages, such as the service's commands or events</typeparam>
    public class MessagePublisher<TMessage> : IMessagePublisher<TMessage> where TMessage : class
    {
        private readonly IMessageSerializer _serializer;
        private readonly string _exchange;
        private readonly string _routingKeyPrefix;
        private Lazy<Task<IChannel>> _channel;

        /// <summary>Creates a publisher to <paramref name="exchange"/> under <paramref name="routingKeyPrefix"/>.</summary>
        /// <param name="connection">The connection to RabbitMQ, opened on the first message</param>
        /// <param name="serializer">Writes each message's body</param>
        /// <param name="exchange">The topic exchange, declared if it doesn't exist</param>
        /// <param name="routingKeyPrefix">The routing key's prefix, for example <c>orders.Events</c></param>
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

        /// <inheritdoc />
        public async Task PublishAsync(TMessage message, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default)
        {
            var body = _serializer.Serialize(message);
            var properties = new BasicProperties {ContentType = "bytes", Headers = ToAmqpHeaders(headers)};
            var routingKey = GetRoutingKey(message);
            var channel = await _channel.Value.ConfigureAwait(false);
            await channel.BasicPublishAsync(_exchange, routingKey, false, properties, body, cancellationToken).ConfigureAwait(false);
        }

        // The Obvs headers (such as trace context) as AMQP headers
        private static IDictionary<string, object> ToAmqpHeaders(IReadOnlyDictionary<string, string> headers)
        {
            return headers.Count == 0 ? null : headers.ToDictionary(header => header.Key, header => (object)header.Value);
        }

        private string GetRoutingKey(TMessage message)
        {
            return string.Format("{0}.{1}", _routingKeyPrefix, message.GetType().Name);
        }

        /// <inheritdoc />
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
