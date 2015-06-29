using System;
using System.Collections.Generic;
using System.Reflection;
using Obvs.Configuration;
using Obvs.Serialization;

namespace Obvs.ActiveMQ.Configuration
{
    public interface ICanSpecifyActiveMQServiceName<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TMessage : class
        where TCommand : class, TMessage
        where TEvent : class, TMessage
        where TRequest : class, TMessage
        where TResponse : class, TMessage
    {
        ICanSpecifyActiveMQBroker<TMessage, TCommand, TEvent, TRequest, TResponse> Named(string serviceName);
    }

    public interface ICanSpecifyActiveMQQueue<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TMessage : class
        where TCommand : class, TMessage
        where TEvent : class, TMessage
        where TRequest : class, TMessage
        where TResponse : class, TMessage
    {
        ICanSpecifyActiveMQBroker<TMessage, TCommand, TEvent, TRequest, TResponse> UsingQueueFor<T>(AcknowledgementMode mode = AcknowledgementMode.AutoAcknowledge) where T : TMessage;
    }

    public interface ICanSpecifyActiveMQBroker<TMessage, TCommand, TEvent, TRequest, TResponse> : ICanSpecifyActiveMQQueue<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TMessage : class
        where TCommand : class, TMessage
        where TEvent : class, TMessage
        where TRequest : class, TMessage
        where TResponse : class, TMessage
    {
        ICanSpecifyEndpointSerializers<TMessage, TCommand, TEvent, TRequest, TResponse> ConnectToBroker(string brokerUri);
    }

    internal class ActiveMQFluentConfig<TServiceMessage, TMessage, TCommand, TEvent, TRequest, TResponse> : 
        ICanSpecifyActiveMQBroker<TMessage, TCommand, TEvent, TRequest, TResponse>, 
        ICanSpecifyActiveMQServiceName<TMessage, TCommand, TEvent, TRequest, TResponse>, 
        ICanCreateEndpointAsClientOrServer<TMessage, TCommand, TEvent, TRequest, TResponse>, 
        ICanSpecifyEndpointSerializers<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TMessage : class
        where TServiceMessage : class 
        where TCommand : class, TMessage 
        where TEvent : class, TMessage
        where TRequest : class, TMessage 
        where TResponse : class, TMessage
    {
        private readonly ICanAddEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse> _canAddEndpoint;
        private string _serviceName;
        private string _brokerUri;
        private IMessageSerializer _serializer;
        private IMessageDeserializerFactory _deserializerFactory;
        private readonly List<Tuple<Type, AcknowledgementMode>> _queueTypes = new List<Tuple<Type, AcknowledgementMode>>();
        private Func<Assembly, bool> _assemblyFilter;
        private Func<Type, bool> _typeFilter;

        public ActiveMQFluentConfig(ICanAddEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse> canAddEndpoint)
        {
            _canAddEndpoint = canAddEndpoint;
        }

        public ICanSpecifyActiveMQBroker<TMessage, TCommand, TEvent, TRequest, TResponse> Named(string serviceName)
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

        private ActiveMQServiceEndpointProvider<TServiceMessage, TMessage, TCommand, TEvent, TRequest, TResponse> CreateProvider()
        {
            return new ActiveMQServiceEndpointProvider<TServiceMessage, TMessage, TCommand, TEvent, TRequest, TResponse>(_serviceName, _brokerUri, _serializer, _deserializerFactory, _queueTypes, _assemblyFilter, _typeFilter);
        }

        public ICanSpecifyEndpointSerializers<TMessage, TCommand, TEvent, TRequest, TResponse> ConnectToBroker(string brokerUri)
        {
            _brokerUri = brokerUri;
            return this;
        }

        public ICanCreateEndpointAsClientOrServer<TMessage, TCommand, TEvent, TRequest, TResponse> SerializedWith(IMessageSerializer serializer, IMessageDeserializerFactory deserializerFactory)
        {
            _serializer = serializer;
            _deserializerFactory = deserializerFactory;
            return this;
        }

        public ICanSpecifyActiveMQBroker<TMessage, TCommand, TEvent, TRequest, TResponse> UsingQueueFor<T>(AcknowledgementMode mode = AcknowledgementMode.AutoAcknowledge) where T : TMessage
        {
            _queueTypes.Add(new Tuple<Type, AcknowledgementMode>(typeof(T), mode));
            return this;
        }
    }
}