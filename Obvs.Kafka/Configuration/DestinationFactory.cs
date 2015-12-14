using System;
using System.Reactive.Concurrency;
using System.Reflection;
using Obvs.MessageProperties;
using Obvs.Serialization;

namespace Obvs.Kafka.Configuration
{
    public static class DestinationFactory
    {
        public static MessagePublisher<TMessage> CreatePublisher<TMessage>(
            KafkaConfiguration kafkaConfiguration, 
            KafkaProducerConfiguration producerConfiguration,
            string topic, 
            IMessageSerializer messageSerializer, 
            IScheduler scheduler,
            IMessagePropertyProvider<TMessage> propertyProvider = null) 
            where TMessage : class
        {
            return new MessagePublisher<TMessage>(
                kafkaConfiguration,
                producerConfiguration,
                topic,
                messageSerializer,
                propertyProvider ?? new DefaultPropertyProvider<TMessage>());
        }

        public static MessageSource<TMessage> CreateSource<TMessage, TServiceMessage>(
            KafkaConfiguration kafkaConfiguration,
            KafkaSourceConfiguration sourceConfiguration,
            string topic, 
            IMessageDeserializerFactory deserializerFactory,
            Func<Assembly, bool> assemblyFilter = null, 
            Func<Type, bool> typeFilter = null)
            where TMessage : class
            where TServiceMessage : class
        {
            return new MessageSource<TMessage>(
                kafkaConfiguration,
                sourceConfiguration,
                topic,
                deserializerFactory.Create<TMessage, TServiceMessage>(assemblyFilter, typeFilter));
        }
    }
}