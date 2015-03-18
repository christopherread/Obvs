using System.Text;
using Obvs.Types;
using RabbitMQ.Client;
using RabbitMQ.Client.Framing;

namespace Obvs.RabbitMQ
{
    public class MessagePublisher<TMessage> : IMessagePublisher<TMessage> where TMessage : IMessage
    {
        private readonly IMessageSerializer _serializer;
        private readonly string _exchange;
        private readonly IConnection _connection;
        private readonly IModel _channel;

        public MessagePublisher(IConnectionFactory connectionFactory, IMessageSerializer serializer, string exchange)
        {
            _serializer = serializer;
            _exchange = exchange;
            _connection = connectionFactory.CreateConnection();
            _channel = _connection.CreateModel();
            _channel.ExchangeDeclare(_exchange, RabbitExchangeTypes.Topic);
        }

        public void Publish(TMessage message)
        {
            string routingKey = string.Format("{0}.{1}", typeof (TMessage).Name, message.GetType().Name);
            object serializedMessage = _serializer.Serialize(message);
            byte[] body = serializedMessage as byte[] ?? Encoding.UTF8.GetBytes((string) serializedMessage);
            string contentType = serializedMessage is byte[] ? "bytes" : "text";
            _channel.BasicPublish(_exchange, routingKey, new BasicProperties { ContentType = contentType }, body);
        }

        public void Dispose()
        {
            if (_channel != null)
            {
                _channel.Close();
                _channel.Dispose();
                _connection.Close();
                _connection.Dispose();
            }
        }
    }
}