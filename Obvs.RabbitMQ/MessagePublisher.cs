using System;
using System.Threading;
using System.Threading.Tasks;
using Obvs.Serialization;
using RabbitMQ.Client;
using RabbitMQ.Client.Framing;

namespace Obvs.RabbitMQ
{
    public class MessagePublisher<TMessage> : IMessagePublisher<TMessage> where TMessage : class
    {
        private readonly IMessageSerializer _serializer;
        private readonly string _exchange;
        private readonly string _routingKeyPrefix;
        private Lazy<IModel> _channel;

        public MessagePublisher(Lazy<IConnection> connection, IMessageSerializer serializer, string exchange,
            string routingKeyPrefix)
        {
            _serializer = serializer;
            _exchange = exchange;
            _routingKeyPrefix = routingKeyPrefix;

            _channel = new Lazy<IModel>(() =>
            {
                var channel = connection.Value.CreateModel();
                channel.ExchangeDeclare(exchange, ExchangeType.Topic);
                return channel;
            }, LazyThreadSafetyMode.ExecutionAndPublication);
        }

        public Task PublishAsync(TMessage message)
        {
            Publish(message);
            return Task.FromResult(true);
        }

        private void Publish(TMessage message)
        {
            var body = _serializer.Serialize(message);
            var properties = new BasicProperties {ContentType = "bytes"};
            var routingKey = GetRoutingKey(message);
            _channel.Value.BasicPublish(_exchange, routingKey, properties, body);
        }

        private string GetRoutingKey(TMessage message)
        {
            return string.Format("{0}.{1}", _routingKeyPrefix, message.GetType().Name);
        }

        public void Dispose()
        {
            if (_channel.IsValueCreated)
            {
                _channel.Value.Close();
                _channel.Value.Dispose();
                _channel = null;
            }
        }
    }
}