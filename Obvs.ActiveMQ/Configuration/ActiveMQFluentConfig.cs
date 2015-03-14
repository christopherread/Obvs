using Obvs.Configuration;
using Obvs.Types;

namespace Obvs.ActiveMQ.Configuration
{
    public interface ICanSpecifyActiveMQServiceName
    {
        ICanSpecifyActiveMQBroker Named(string serviceName);
    }

    public interface ICanSpecifyActiveMQBroker
    {
        ICanSpecifyEndpointSerializers UsingBroker(string brokerUri);
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

        public ICanAddEndpointOrCreate AsClient()
        {
            return _canAddEndpoint.WithClientEndpoints(CreateProvider());
        }

        public ICanAddEndpointOrCreate AsServer()
        {
            return _canAddEndpoint.WithServerEndpoints(CreateProvider());
        }

        public ICanAddEndpointOrCreate AsClientAndServer()
        {
            return _canAddEndpoint.WithEndpoints(CreateProvider());
        }

        private ActiveMQServiceEndpointProvider<TServiceMessage> CreateProvider()
        {
            return new ActiveMQServiceEndpointProvider<TServiceMessage>(_serviceName, _brokerUri, _serializer, _deserializerFactory, _assemblyNameContains);
        }

        public ICanSpecifyEndpointSerializers UsingBroker(string brokerUri)
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
    }
}