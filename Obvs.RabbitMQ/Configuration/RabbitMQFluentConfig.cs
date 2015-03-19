using Obvs.Configuration;
using Obvs.Types;

namespace Obvs.RabbitMQ.Configuration
{
    public interface ICanSpecifyRabbitMQServiceName
    {
        ICanSpecifyRabbitMQBroker Named(string serviceName);
    }

    public interface ICanSpecifyRabbitMQBroker
    {
        ICanSpecifyEndpointSerializers ConnectToBroker(string brokerUri);
    }

    public class RabbitMQFluentConfig<TServiceMessage> : ICanSpecifyRabbitMQServiceName, ICanSpecifyRabbitMQBroker, ICanCreateEndpointAsClientOrServer, ICanSpecifyEndpointSerializers where TServiceMessage : IMessage
    {
        private readonly ICanAddEndpoint _canAddEndpoint;
        private string _serviceName;
        private string _brokerUri;
        private string _assemblyNameContains;
        private IMessageSerializer _serializer;
        private IMessageDeserializerFactory _deserializerFactory;

        public RabbitMQFluentConfig(ICanAddEndpoint canAddEndpoint)
        {
            _canAddEndpoint = canAddEndpoint;
        }

        public ICanSpecifyRabbitMQBroker Named(string serviceName)
        {
            _serviceName = serviceName;
            return this;
        }

        public ICanSpecifyEndpointSerializers ConnectToBroker(string brokerUri)
        {
            _brokerUri = brokerUri;
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

        private RabbitMQServiceEndpointProvider<TServiceMessage> CreateProvider()
        {
            return new RabbitMQServiceEndpointProvider<TServiceMessage>(_serviceName, _brokerUri,
                _serializer, _deserializerFactory, _assemblyNameContains);
        }

        public ICanAddEndpointOrCreate AsServer()
        {
            return _canAddEndpoint.WithServerEndpoints(CreateProvider());
        }

        public ICanAddEndpointOrCreate AsClientAndServer()
        {
            return _canAddEndpoint.WithEndpoints(CreateProvider());
        }

        public ICanCreateEndpointAsClientOrServer SerializedWith(IMessageSerializer serializer,
            IMessageDeserializerFactory deserializerFactory)
        {
            _serializer = serializer;
            _deserializerFactory = deserializerFactory;
            return this;
        }
    }
}