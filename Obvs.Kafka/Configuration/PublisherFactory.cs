using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using Obvs.Serialization;

// v6's Kafka transport building blocks, which UseKafka(...) is built on: its public members are exempt from the rule that every one is documented
#pragma warning disable CS1570, CS1572, CS1591

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