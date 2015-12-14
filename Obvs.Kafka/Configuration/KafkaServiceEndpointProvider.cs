using System;
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

        public KafkaServiceEndpointProvider(string serviceName,
                                               KafkaConfiguration kafkaConfiguration,
                                               KafkaSourceConfiguration sourceConfiguration,
                                               KafkaProducerConfiguration producerConfiguration,
                                               IMessageSerializer serializer,
                                               IMessageDeserializerFactory deserializerFactory,
                                               Func<Assembly, bool> assemblyFilter = null,
                                               Func<Type, bool> typeFilter = null)
            : base(serviceName)
        {
            _kafkaConfiguration = kafkaConfiguration;
            _sourceConfiguration = sourceConfiguration ?? new KafkaSourceConfiguration();
            _producerConfiguration = producerConfiguration ?? new KafkaProducerConfiguration();
            _serializer = serializer;
            _deserializerFactory = deserializerFactory;
            _assemblyFilter = assemblyFilter;
            _typeFilter = typeFilter;

            if (string.IsNullOrEmpty(_kafkaConfiguration?.SeedAddresses))
            {
                throw new InvalidOperationException(string.Format("For service endpoint '{0}', please specify a kafkaUri to connect to. To do this you can use ConnectToKafka() per endpoint", serviceName));
            }
        }

        public override IServiceEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse> CreateEndpoint()
        {
            return new ServiceEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse>(
                    CreateSource<TRequest>(_kafkaConfiguration, _sourceConfiguration, RequestsDestination),
                    CreateSource<TCommand>(_kafkaConfiguration, _sourceConfiguration, CommandsDestination),
                    CreatePublisher<TEvent>(_kafkaConfiguration, _producerConfiguration, EventsDestination),
                    CreatePublisher<TResponse>(_kafkaConfiguration, _producerConfiguration, ResponsesDestination),
                    typeof(TServiceMessage));
        }

        private IMessageSource<T> CreateSource<T>(KafkaConfiguration kafkaConfiguration, KafkaSourceConfiguration sourceConfiguration, string topic) where T : class, TMessage
        {
            return DestinationFactory.CreateSource<T, TServiceMessage>(kafkaConfiguration, sourceConfiguration, topic, _deserializerFactory, _assemblyFilter, _typeFilter);
        }

        private IMessagePublisher<T> CreatePublisher<T>(KafkaConfiguration kafkaConfiguration, KafkaProducerConfiguration producerConfiguration, string topic) where T : class, TMessage
        {
            return DestinationFactory.CreatePublisher<T>(kafkaConfiguration, producerConfiguration, topic, _serializer, null, null);
        }


        public override IServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse> CreateEndpointClient()
        {
            return new ServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse>(
                    CreateSource<TEvent>(_kafkaConfiguration, _sourceConfiguration, EventsDestination),
                    CreateSource<TResponse>(_kafkaConfiguration, _sourceConfiguration, ResponsesDestination),
                    CreatePublisher<TRequest>(_kafkaConfiguration, _producerConfiguration, RequestsDestination),
                    CreatePublisher<TCommand>(_kafkaConfiguration, _producerConfiguration, CommandsDestination),
                    typeof(TServiceMessage));
        }

    }
}