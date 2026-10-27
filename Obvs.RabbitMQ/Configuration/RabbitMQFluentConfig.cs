using System;
using System.Reflection;
using Obvs.Configuration;
using Obvs.Serialization;

// v6's RabbitMQ transport building blocks, public for v6's fluent configuration; v7 apps use UseRabbitMq(...): its public members are exempt from the rule that every one is documented
#pragma warning disable CS1570, CS1572, CS1591

namespace Obvs.RabbitMQ.Configuration
{
    public interface ICanSpecifyRabbitMQServiceName<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TMessage : class
        where TCommand : class, TMessage
        where TEvent : class, TMessage
        where TRequest : class, TMessage
        where TResponse : class, TMessage
    {
        ICanSpecifyRabbitMQBroker<TMessage, TCommand, TEvent, TRequest, TResponse> Named(string serviceName);
    }

    public interface ICanSpecifyRabbitMQBroker<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TMessage : class
        where TCommand : class, TMessage
        where TEvent : class, TMessage
        where TRequest : class, TMessage
        where TResponse : class, TMessage
    {
        ICanSpecifyEndpointSerializers<TMessage, TCommand, TEvent, TRequest, TResponse> ConnectToBroker(string brokerUri);
    }

    public class RabbitMQFluentConfig<TServiceMessage, TMessage, TCommand, TEvent, TRequest, TResponse> : 
        ICanSpecifyRabbitMQServiceName<TMessage, TCommand, TEvent, TRequest, TResponse>, 
        ICanSpecifyRabbitMQBroker<TMessage, TCommand, TEvent, TRequest, TResponse>, 
        ICanCreateEndpointAsClientOrServer<TMessage, TCommand, TEvent, TRequest, TResponse>, 
        ICanSpecifyEndpointSerializers<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TServiceMessage : class
        where TMessage : class
        where TCommand : class, TMessage
        where TEvent : class, TMessage
        where TRequest : class, TMessage
        where TResponse : class, TMessage
    {
        private readonly ICanAddEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse> _canAddEndpoint;
        private string _serviceName;
        private string _brokerUri;
        private Func<Assembly, bool> _assemblyFilter;
        private IMessageSerializer _serializer;
        private IMessageDeserializerFactory _deserializerFactory;
        private Func<Type, bool> _typeFilter;

        public RabbitMQFluentConfig(ICanAddEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse> canAddEndpoint)
        {
            _canAddEndpoint = canAddEndpoint;
        }

        public ICanSpecifyRabbitMQBroker<TMessage, TCommand, TEvent, TRequest, TResponse> Named(string serviceName)
        {
            _serviceName = serviceName;
            return this;
        }

        public ICanSpecifyEndpointSerializers<TMessage, TCommand, TEvent, TRequest, TResponse> ConnectToBroker(string brokerUri)
        {
            _brokerUri = brokerUri;
            return this;
        }

        public ICanAddEndpointOrLoggingOrCorrelationOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse> AsClient()
        {
            return _canAddEndpoint.WithClientEndpoints(CreateProvider());
        }

        private IServiceEndpointProvider<TMessage, TCommand, TEvent, TRequest, TResponse> CreateProvider()
        {
            return new RabbitMQServiceEndpointProvider<TServiceMessage, TMessage, TCommand, TEvent, TRequest, TResponse>(_serviceName, _brokerUri,
                _serializer, _deserializerFactory, _assemblyFilter, _typeFilter);
        }

        public ICanAddEndpointOrLoggingOrCorrelationOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse> AsServer()
        {
            return _canAddEndpoint.WithServerEndpoints(CreateProvider());
        }

        public ICanAddEndpointOrLoggingOrCorrelationOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse> AsClientAndServer()
        {
            return _canAddEndpoint.WithEndpoints(CreateProvider());
        }

        public ICanCreateEndpointAsClientOrServer<TMessage, TCommand, TEvent, TRequest, TResponse> SerializedWith(IMessageSerializer serializer,
            IMessageDeserializerFactory deserializerFactory)
        {
            _serializer = serializer;
            _deserializerFactory = deserializerFactory;
            return this;
        }

        public ICanCreateEndpointAsClientOrServer<TMessage, TCommand, TEvent, TRequest, TResponse> FilterMessageTypeAssemblies(Func<Assembly, bool> assemblyFilter = null, Func<Type, bool> typeFilter = null)
        {
            _assemblyFilter = assemblyFilter;
            _typeFilter = typeFilter;
            return this;
        }
    }
}