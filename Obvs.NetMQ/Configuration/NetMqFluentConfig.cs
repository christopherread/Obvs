using System;
using System.Reflection;
using Obvs.Configuration;
using Obvs.Serialization;
using Obvs.Types;

namespace Obvs.NetMQ.Configuration
{
    public interface ICanAddNetMqServiceName<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TMessage : class
        where TCommand : class, TMessage
        where TEvent : class, TMessage
        where TRequest : class, TMessage
        where TResponse : class, TMessage
    {
        ICanAddNetMqAddress<TMessage, TCommand, TEvent, TRequest, TResponse> Named(string serviceName);
    }

    public interface ICanAddNetMqAddress<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TMessage : class
        where TCommand : class, TMessage
        where TEvent : class, TMessage
        where TRequest : class, TMessage
        where TResponse : class, TMessage
    {
        ICanAddNetMqPort<TMessage, TCommand, TEvent, TRequest, TResponse> BindToAddress(string address);
        ICanSpecifyEndpointSerializers<TMessage, TCommand, TEvent, TRequest, TResponse> BindToNonTcpAddress(string address);
    }

    public interface ICanAddNetMqPort<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TMessage : class
        where TCommand : class, TMessage
        where TEvent : class, TMessage
        where TRequest : class, TMessage
        where TResponse : class, TMessage
    {
        ICanSpecifyEndpointSerializers<TMessage, TCommand, TEvent, TRequest, TResponse> OnPort(int port);
    }

    internal class NetMqFluentConfig<TServiceMessage, TMessage, TCommand, TEvent, TRequest, TResponse> :
        ICanAddNetMqAddress<TMessage, TCommand, TEvent, TRequest, TResponse>,
        ICanAddNetMqPort<TMessage, TCommand, TEvent, TRequest, TResponse>,
        ICanAddNetMqServiceName<TMessage, TCommand, TEvent, TRequest, TResponse>,
        ICanCreateEndpointAsClientOrServer<TMessage, TCommand, TEvent, TRequest, TResponse>,
        ICanSpecifyEndpointSerializers<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TServiceMessage : class, TMessage
        where TMessage : class
        where TCommand : class, TMessage
        where TEvent : class, TMessage
        where TRequest : class, TMessage
        where TResponse : class, TMessage
    {
        private readonly ICanAddEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse> _canAddEndpoint;
        private string _serviceName;
        private Uri _address;
        private int _port;
        private IMessageSerializer _serializer;
        private IMessageDeserializerFactory _deserializerFactory;
        private Func<Assembly, bool> _assemblyFilter;
        private Func<Type, bool> _typeFilter;

        public NetMqFluentConfig(ICanAddEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse> canAddEndpoint)
        {
            _canAddEndpoint = canAddEndpoint;
        }

        public ICanAddNetMqAddress<TMessage, TCommand, TEvent, TRequest, TResponse> Named(string serviceName)
        {
            _serviceName = serviceName;
            return this;
        }

        public ICanAddNetMqPort<TMessage, TCommand, TEvent, TRequest, TResponse> BindToAddress(string address)
        {
            _address = new Uri(address);
            return this;
        }

        public ICanSpecifyEndpointSerializers<TMessage, TCommand, TEvent, TRequest, TResponse> BindToNonTcpAddress(string address)
        {
            _address = new Uri(address);
            return this;
        }

        public ICanSpecifyEndpointSerializers<TMessage, TCommand, TEvent, TRequest, TResponse> OnPort(int port)
        {
            _port = port;
            return this;
        }

        private NetMqServiceEndpointProvider<TServiceMessage, TMessage, TCommand, TEvent, TRequest, TResponse> CreateProvider()
        {
            return new NetMqServiceEndpointProvider<TServiceMessage, TMessage, TCommand, TEvent, TRequest, TResponse>(
                _serviceName,
                _address.OriginalString,
                _port,
                _serializer,
                _deserializerFactory,
                _assemblyFilter,
                _typeFilter);
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
