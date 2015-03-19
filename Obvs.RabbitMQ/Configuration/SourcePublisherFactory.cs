using Obvs.Types;
using RabbitMQ.Client;

namespace Obvs.RabbitMQ.Configuration
{
    internal static class SourcePublisherFactory
    {
        public static MessagePublisher<TMessage> CreatePublisher<TMessage>(string brokerUri, string routingKeyPrefix, string serviceName, IMessageSerializer messageSerializer)
            where TMessage : IMessage
        {
            return new MessagePublisher<TMessage>(
                CreateConnectionFactory(brokerUri),
                messageSerializer,
                serviceName,
                routingKeyPrefix);
        }

        public static MessageSource<TMessage> CreateSource<TMessage, TServiceMessage>(string brokerUri, string routingKeyPrefix, string serviceName, IMessageDeserializerFactory deserializerFactory, string assemblyNameContains = null, string selector = null)
            where TServiceMessage : IMessage
            where TMessage : IMessage
        {
            return new MessageSource<TMessage>(
                CreateConnectionFactory(brokerUri),
                deserializerFactory.Create<TMessage, TServiceMessage>(assemblyNameContains), 
                serviceName,
                routingKeyPrefix);
        }

        private static ConnectionFactory CreateConnectionFactory(string brokerUri)
        {
            return new ConnectionFactory { Uri = brokerUri, AutomaticRecoveryEnabled = true, };
        }
    }
}