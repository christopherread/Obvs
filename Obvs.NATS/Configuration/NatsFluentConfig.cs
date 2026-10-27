using System;
using System.Reflection;
using Obvs.Configuration;
using Obvs.Serialization;

// v6's NATS transport building blocks, public for v6's fluent configuration; v7 apps use UseNats(...): its public members are exempt from the rule that every one is documented
#pragma warning disable CS1570, CS1572, CS1591

namespace Obvs.NATS.Configuration
{
    public class NatsFluentConfig<TServiceMessage, TMessage, TCommand, TEvent, TRequest, TResponse> :
        ICanCreateEndpointAsClientOrServer<TMessage, TCommand, TEvent, TRequest, TResponse>,
        ICanSpecifyEndpointSerializers<TMessage, TCommand, TEvent, TRequest, TResponse> where TMessage : class
        where TCommand : class, TMessage
        where TEvent : class, TMessage
        where TRequest : class, TMessage
        where TResponse : class, TMessage where TServiceMessage : class
    {
        private readonly ICanAddEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse> _canAddEndpoint;
        private readonly NatsEndpointSettings<TMessage> _settings;
        private IMessageSerializer _serializer;
        private IMessageDeserializerFactory _deserializerFactory;
        private Func<Assembly, bool> _assemblyFilter;
        private Func<Type, bool> _typeFilter;

        public NatsFluentConfig(ICanAddEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse> canAddEndpoint)
        {
            _canAddEndpoint = canAddEndpoint;
        }

        public NatsFluentConfig(ICanAddEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse> canAddEndpoint,
            NatsEndpointSettings<TMessage> settings)
        {
            _canAddEndpoint = canAddEndpoint;
            _settings = settings;
        }

        public ICanAddEndpointOrLoggingOrCorrelationOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse> AsClient()
        {
            return _canAddEndpoint.WithClientEndpoints(CreateServiceEndpointProvider());
        }

        private NatsServiceEndpointProvider<TServiceMessage, TMessage, TCommand, TEvent, TRequest, TResponse> CreateServiceEndpointProvider()
        {
            return new NatsServiceEndpointProvider<TServiceMessage, TMessage, TCommand, TEvent, TRequest, TResponse>(_settings,
                _serializer, _deserializerFactory,
                
                _assemblyFilter, 
                _typeFilter, propertyFilter: _settings.MessageProperties.Filter, propertyProvider: _settings.MessageProperties.Provider);
        }

        public ICanAddEndpointOrLoggingOrCorrelationOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse>
            AsClientAndServer()
        {
            return _canAddEndpoint.WithEndpoints(CreateServiceEndpointProvider());
        }

        public ICanAddEndpointOrLoggingOrCorrelationOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse> AsServer()
        {
            return _canAddEndpoint.WithServerEndpoints(CreateServiceEndpointProvider());
        }

        public ICanCreateEndpointAsClientOrServer<TMessage, TCommand, TEvent, TRequest, TResponse>
            FilterMessageTypeAssemblies(Func<Assembly, bool> assemblyFilter = null, Func<Type, bool> typeFilter = null)
        {
            _assemblyFilter = assemblyFilter;
            _typeFilter = typeFilter;
            return this;
        }

        public ICanCreateEndpointAsClientOrServer<TMessage, TCommand, TEvent, TRequest, TResponse> SerializedWith(
            IMessageSerializer serializer,
            IMessageDeserializerFactory deserializerFactory)
        {
            _serializer = serializer;
            _deserializerFactory = deserializerFactory;
            return this;
        }
    }
}