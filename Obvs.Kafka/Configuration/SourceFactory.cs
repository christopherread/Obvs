using System;
using System.Collections.Generic;
using System.Reflection;
using Obvs.Serialization;

namespace Obvs.Kafka.Configuration
{
    public static class SourceFactory
    {
        public static MessageSource<TMessage> Create<TMessage, TServiceMessage>(
            KafkaConfiguration kafkaConfiguration, 
            KafkaSourceConfiguration sourceConfiguration, 
            string topic, 
            IMessageDeserializerFactory deserializerFactory,
            Func<Dictionary<string, string>, bool> propertyFilter, 
            Func<Assembly, bool> assemblyFilter = null, 
            Func<Type, bool> typeFilter = null)
            where TMessage : class
            where TServiceMessage : class
        {
            return new MessageSource<TMessage>(
                kafkaConfiguration,
                sourceConfiguration,
                topic,
                deserializerFactory.Create<TMessage, TServiceMessage>(assemblyFilter, typeFilter),
                propertyFilter);
        }
    }
}