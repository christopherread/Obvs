using System;
using System.Collections.Generic;
using Obvs.Configuration;
using Obvs.Serialization;
using Obvs.Types;

namespace Obvs.ActiveMQ.Configuration
{
    public interface ICanSpecifyActiveMQServiceName
    {
        ICanSpecifyActiveMQBroker Named(string serviceName);
    }

    public interface ICanSpecifyActiveMQQueue
    {
        ICanSpecifyActiveMQBroker UsingQueueFor<TMessage>() where TMessage : IMessage;
    }

    public interface ICanSpecifyActiveMQBroker : ICanSpecifyActiveMQQueue
    {
        ICanSpecifyEndpointSerializers ConnectToBroker(string brokerUri);
    }

    internal class ActiveMQFluentConfig<TServiceMessage> : ICanSpecifyActiveMQBroker, ICanSpecifyActiveMQServiceName, ICanCreateEndpointAsClientOrServer, ICanSpecifyEndpointSerializers
        where TServiceMessage : IMessage
    {
        private readonly ICanAddEndpoint _canAddEndpoint;
        private string _serviceName;
        private string _brokerUri;
        private string _assemblyNameContains = string.Empty;
        private IMessageSerializer _serializer;
        private IMessageDeserializerFactory _deserializerFactory;
        private readonly List<Type> _queueTypes = new List<Type>();

        public ActiveMQFluentConfig(ICanAddEndpoint canAddEndpoint)
        {
            _canAddEndpoint = canAddEndpoint;
        }

        public ICanSpecifyActiveMQBroker Named(string serviceName)
        {
            _serviceName = serviceName;
            return this;
        }

        public ICanCreateEndpointAsClientOrServer FilterMessageTypeAssemblies(string assemblyNameContains)
        {
            _assemblyNameContains = assemblyNameContains;
            return this;
        }

        public ICanAddEndpointOrLoggingOrCreate AsClient()
        {
            return _canAddEndpoint.WithClientEndpoints(CreateProvider());
        }

        public ICanAddEndpointOrLoggingOrCreate AsServer()
        {
            return _canAddEndpoint.WithServerEndpoints(CreateProvider());
        }

        public ICanAddEndpointOrLoggingOrCreate AsClientAndServer()
        {
            return _canAddEndpoint.WithEndpoints(CreateProvider());
        }

        private ActiveMQServiceEndpointProvider<TServiceMessage> CreateProvider()
        {
            return new ActiveMQServiceEndpointProvider<TServiceMessage>(_serviceName, _brokerUri, _serializer, _deserializerFactory, _queueTypes, _assemblyNameContains);
        }

        public ICanSpecifyEndpointSerializers ConnectToBroker(string brokerUri)
        {
            _brokerUri = brokerUri;
            return this;
        }

        public ICanCreateEndpointAsClientOrServer SerializedWith(IMessageSerializer serializer, IMessageDeserializerFactory deserializerFactory)
        {
            _serializer = serializer;
            _deserializerFactory = deserializerFactory;
            return this;
        }

        public ICanSpecifyActiveMQBroker UsingQueueFor<TMessage>() where TMessage : IMessage
        {
            _queueTypes.Add(typeof(TMessage));
            return this;
        }
    }
}