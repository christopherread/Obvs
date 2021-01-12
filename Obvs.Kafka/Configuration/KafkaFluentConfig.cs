using System;
using System.Collections.Generic;
using System.Reflection;
using Obvs.Configuration;
using Obvs.Serialization;

namespace Obvs.Kafka.Configuration
{
    public interface ICanSpecifyKafkaServiceName<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TMessage : class
        where TCommand : class, TMessage
        where TEvent : class, TMessage
        where TRequest : class, TMessage
        where TResponse : class, TMessage
    {
        ICanSpecifyKafkaBroker<TMessage, TCommand, TEvent, TRequest, TResponse> Named(string serviceName);
    }


    public interface ICanSpecifyKafkaBroker<TMessage, TCommand, TEvent, TRequest, TResponse> : 
        ICanSpecifyKafkaServiceName<TMessage, TCommand, TEvent, TRequest, TResponse>, 
        ICanSpecifyEndpointSerializers<TMessage, TCommand, TEvent, TRequest, TResponse>,
        ICanSpecifyKafkaMessageFiltering<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TMessage : class
        where TCommand : class, TMessage
        where TEvent : class, TMessage
        where TRequest : class, TMessage
        where TResponse : class, TMessage
    {
        ICanSpecifyKafkaBroker<TMessage, TCommand, TEvent, TRequest, TResponse> WithKafkaProducerConfiguration(KafkaProducerConfiguration kafkaProducerConfiguration);
        ICanSpecifyKafkaBroker<TMessage, TCommand, TEvent, TRequest, TResponse> WithKafkaSourceConfiguration(KafkaSourceConfiguration kafkaSourceConfiguration);
        ICanSpecifyEndpointSerializers<TMessage, TCommand, TEvent, TRequest, TResponse> ConnectToKafka(string connectionString);
    }

    public interface ICanSpecifyKafkaMessageFiltering<TMessage, TCommand, TEvent, TRequest, TResponse>
       where TMessage : class
       where TCommand : class, TMessage
       where TEvent : class, TMessage
       where TRequest : class, TMessage
       where TResponse : class, TMessage
    {
        ICanSpecifyKafkaBroker<TMessage, TCommand, TEvent, TRequest, TResponse> FilterReceivedMessages(
            Func<Dictionary<string, string>, bool> propertyFilter);

        ICanSpecifyKafkaBroker<TMessage, TCommand, TEvent, TRequest, TResponse> AppendMessageProperties(
            Func<TMessage, Dictionary<string, string>> propertyProvider);
    }

    internal class KafkaFluentConfig<TServiceMessage, TMessage, TCommand, TEvent, TRequest, TResponse> :
        ICanSpecifyKafkaBroker<TMessage, TCommand, TEvent, TRequest, TResponse>,
        ICanCreateEndpointAsClientOrServer<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TMessage : class
        where TServiceMessage : class 
        where TCommand : class, TMessage 
        where TEvent : class, TMessage
        where TRequest : class, TMessage 
        where TResponse : class, TMessage
    {
        private readonly ICanAddEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse> _canAddEndpoint;
        private string _serviceName;
        private string _connectiongString;
        private IMessageSerializer _serializer;
        private IMessageDeserializerFactory _deserializerFactory;
        private Func<Assembly, bool> _assemblyFilter;
        private Func<Type, bool> _typeFilter;
        private KafkaProducerConfiguration _kafkaProducerConfig;
        private KafkaSourceConfiguration _kafkaSourceConfig;
        private Func<TMessage, Dictionary<string, string>> _propertyProvider;
        private Func<Dictionary<string, string>, bool> _propertyFilter;

        public KafkaFluentConfig(ICanAddEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse> canAddEndpoint)
        {
            _canAddEndpoint = canAddEndpoint;
        }

        public ICanSpecifyKafkaBroker<TMessage, TCommand, TEvent, TRequest, TResponse> Named(string serviceName)
        {
            _serviceName = serviceName;
            return this;
        }

        public ICanCreateEndpointAsClientOrServer<TMessage, TCommand, TEvent, TRequest, TResponse> FilterMessageTypeAssemblies(Func<Assembly, bool> assemblyFilter = null, Func<Type, bool> typeFilter = null)
        {
            _assemblyFilter = assemblyFilter;
            _typeFilter = typeFilter;
            return this;
        }

        public ICanSpecifyKafkaBroker<TMessage, TCommand, TEvent, TRequest, TResponse> WithKafkaProducerConfiguration(KafkaProducerConfiguration kafkaProducerConfiguration)
        {
            _kafkaProducerConfig = kafkaProducerConfiguration;
            return this;
        }

        public ICanSpecifyKafkaBroker<TMessage, TCommand, TEvent, TRequest, TResponse> WithKafkaSourceConfiguration(KafkaSourceConfiguration kafkaSourceConfiguration)
        {
            _kafkaSourceConfig = kafkaSourceConfiguration;
            return this;
        }

        public ICanAddEndpointOrLoggingOrCorrelationOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse> AsClient()
        {
            return _canAddEndpoint.WithClientEndpoints(CreateProvider());
        }

        public ICanAddEndpointOrLoggingOrCorrelationOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse> AsServer()
        {
            return _canAddEndpoint.WithServerEndpoints(CreateProvider());
        }

        public ICanAddEndpointOrLoggingOrCorrelationOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse> AsClientAndServer()
        {
            return _canAddEndpoint.WithEndpoints(CreateProvider());
        }

        private KafkaServiceEndpointProvider<TServiceMessage, TMessage, TCommand, TEvent, TRequest, TResponse> CreateProvider()
        {
            return new KafkaServiceEndpointProvider<TServiceMessage, TMessage, TCommand, TEvent, TRequest, TResponse>(
                _serviceName, 
                new KafkaConfiguration(_connectiongString),
                _kafkaSourceConfig,
                _kafkaProducerConfig,
                _serializer, 
                _deserializerFactory, 
                _propertyFilter,
                _propertyProvider, 
                _assemblyFilter, 
                _typeFilter);
        }

        public ICanSpecifyEndpointSerializers<TMessage, TCommand, TEvent, TRequest, TResponse> ConnectToKafka(string connectionString)
        {
            _connectiongString = connectionString;
            return this;
        }
        
        public ICanCreateEndpointAsClientOrServer<TMessage, TCommand, TEvent, TRequest, TResponse> SerializedWith(IMessageSerializer serializer, IMessageDeserializerFactory deserializerFactory)
        {
            _serializer = serializer;
            _deserializerFactory = deserializerFactory;
            return this;
        }

        public ICanSpecifyKafkaBroker<TMessage, TCommand, TEvent, TRequest, TResponse> FilterReceivedMessages(Func<Dictionary<string, string>, bool> propertyFilter)
        {
            _propertyFilter = propertyFilter;
            return this;
        }

        public ICanSpecifyKafkaBroker<TMessage, TCommand, TEvent, TRequest, TResponse> AppendMessageProperties(Func<TMessage, Dictionary<string, string>> propertyProvider)
        {
            _propertyProvider = propertyProvider;
            return this;
        }
    }
}