using System;
using System.Reflection;
using Obvs.Serialization;
using RabbitMQ.Client;

namespace Obvs.RabbitMQ.Configuration
{
    internal static class SourcePublisherFactory
    {
        public static MessagePublisher<TMessage> CreatePublisher<TMessage>(string brokerUri, string routingKeyPrefix, string serviceName, IMessageSerializer messageSerializer)
            where TMessage : class
        {
            return new MessagePublisher<TMessage>(
                CreateConnectionFactory(brokerUri),
                messageSerializer,
                serviceName,
                routingKeyPrefix);
        }

        public static MessageSource<TMessage> CreateSource<TMessage, TServiceMessage>(string brokerUri, string routingKeyPrefix, string serviceName, IMessageDeserializerFactory deserializerFactory, Func<Assembly, bool> assemblyFilter = null, Func<Type, bool> typeFilter = null, string selector = null)
            where TServiceMessage : class
            where TMessage : class
        {
            return new MessageSource<TMessage>(
                CreateConnectionFactory(brokerUri),
                deserializerFactory.Create<TMessage, TServiceMessage>(assemblyFilter, typeFilter), 
                serviceName,
                routingKeyPrefix);
        }

        private static ConnectionFactory CreateConnectionFactory(string brokerUri)
        {
            return new ConnectionFactory { Uri = brokerUri, AutomaticRecoveryEnabled = true, };
        }
    }
}