using System;
using System.Collections.Generic;
using System.Reflection;
using Obvs.Configuration;
using Obvs.Serialization;

namespace Obvs.Kafka.Configuration
{
    public class KafkaServiceEndpointProvider<TServiceMessage, TMessage, TCommand, TEvent, TRequest, TResponse> : ServiceEndpointProviderBase<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TMessage : class
        where TServiceMessage : class
        where TCommand : class, TMessage
        where TEvent : class, TMessage
        where TRequest : class, TMessage
        where TResponse : class, TMessage
    {
        private readonly KafkaConfiguration _kafkaConfiguration;
        private readonly KafkaSourceConfiguration _sourceConfiguration;
        private readonly KafkaProducerConfiguration _producerConfiguration;
        private readonly IMessageSerializer _serializer;
        private readonly IMessageDeserializerFactory _deserializerFactory;
        private readonly Func<Assembly, bool> _assemblyFilter;
        private readonly Func<Type, bool> _typeFilter;
        private readonly Func<Dictionary<string, string>, bool> _propertyFiter;
        private readonly Func<TMessage, Dictionary<string, string>> _propertyProvider;

        public KafkaServiceEndpointProvider(string serviceName, 
            KafkaConfiguration kafkaConfiguration, KafkaSourceConfiguration sourceConfiguration, 
            KafkaProducerConfiguration producerConfiguration, 
            IMessageSerializer serializer, IMessageDeserializerFactory deserializerFactory,
            Func<Dictionary<string, string>, bool> propertyFilter, 
            Func<TMessage, Dictionary<string, string>> propertyProvider, 
            Func<Assembly, bool> assemblyFilter = null, Func<Type, bool> typeFilter = null)
            : base(serviceName)
        {
            _kafkaConfiguration = kafkaConfiguration;
            _sourceConfiguration = sourceConfiguration ?? new KafkaSourceConfiguration();
            _producerConfiguration = producerConfiguration ?? new KafkaProducerConfiguration();
            _serializer = serializer;
            _deserializerFactory = deserializerFactory;
            _assemblyFilter = assemblyFilter;
            _typeFilter = typeFilter;
            _propertyFiter = propertyFilter;
            _propertyProvider = propertyProvider;

            if (string.IsNullOrEmpty(_kafkaConfiguration?.BootstrapServers))
            {
                throw new InvalidOperationException(string.Format("For service endpoint '{0}', please specify a kafkaUri to connect to. To do this you can use ConnectToKafka() per endpoint", serviceName));
            }
        }

        public override IServiceEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse> CreateEndpoint()
        {
            return new ServiceEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse>(
                    CreateSource<TRequest>(_kafkaConfiguration, _sourceConfiguration, RequestsDestination),
                    CreateSource<TCommand>(_kafkaConfiguration, _sourceConfiguration, CommandsDestination),
                    Create<TEvent>(_kafkaConfiguration, _producerConfiguration, EventsDestination),
                    Create<TResponse>(_kafkaConfiguration, _producerConfiguration, ResponsesDestination),
                    typeof(TServiceMessage));
        }

        private IMessageSource<T> CreateSource<T>(KafkaConfiguration kafkaConfiguration, KafkaSourceConfiguration sourceConfiguration, string topic) where T : class, TMessage
        {
            return SourceFactory.Create<T, TServiceMessage>(kafkaConfiguration, sourceConfiguration, 
                topic, _deserializerFactory, _propertyFiter, _assemblyFilter, _typeFilter);
        }

        private IMessagePublisher<T> Create<T>(KafkaConfiguration kafkaConfiguration, KafkaProducerConfiguration producerConfiguration, string topic) where T : class, TMessage
        {
            return PublisherFactory.CreatePublisher<T>(kafkaConfiguration, producerConfiguration, topic, _serializer, null, _propertyProvider);
        }

        public override IServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse> CreateEndpointClient()
        {
            return new ServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse>(
                    CreateSource<TEvent>(_kafkaConfiguration, _sourceConfiguration, EventsDestination),
                    CreateSource<TResponse>(_kafkaConfiguration, _sourceConfiguration, ResponsesDestination),
                    Create<TRequest>(_kafkaConfiguration, _producerConfiguration, RequestsDestination),
                    Create<TCommand>(_kafkaConfiguration, _producerConfiguration, CommandsDestination),
                    typeof(TServiceMessage));
        }

    }
}