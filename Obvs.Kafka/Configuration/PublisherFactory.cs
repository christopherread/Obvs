using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using Obvs.Serialization;

namespace Obvs.Kafka.Configuration
{
    public static class PublisherFactory
    {
        public static MessagePublisher<TMessage> CreatePublisher<TMessage>(
            KafkaConfiguration kafkaConfiguration,
            KafkaProducerConfiguration producerConfiguration,
            string topic,
            IMessageSerializer messageSerializer,
            IScheduler scheduler,
            Func<TMessage, Dictionary<string, string>> propertyProvider)
            where TMessage : class
        {
            return new MessagePublisher<TMessage>(
                kafkaConfiguration,
                producerConfiguration,
                topic,
                messageSerializer,
                propertyProvider);
        }
    }
}