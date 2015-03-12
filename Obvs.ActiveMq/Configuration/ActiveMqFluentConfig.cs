using Obvs.Configuration;
using Obvs.Types;

namespace Obvs.ActiveMq.Configuration
{
    public interface ICanSpecifyActiveMqServiceName
    {
        ICanSpecifyActiveMqBroker Named(string serviceName);
    }

    public interface ICanSpecifyActiveMqBroker
    {
        ICanCreateClientOrServer UsingBroker(string brokerUri);
    }

    public interface ICanFilterMessageTypeAssemblies
    {
        ICanCreateClientOrServer FilterMessageTypeAssemblies(string assemblyNameContains);
    }

    public interface ICanCreateClientOrServer : ICanFilterMessageTypeAssemblies
    {
        ICanAddEndpointOrCreate AsClient();
        ICanAddEndpointOrCreate AsServer();
        ICanAddEndpointOrCreate AsClientAndServer();
    }

    internal class ActiveMqFluentConfig<TServiceMessage> : ICanSpecifyActiveMqBroker, ICanSpecifyActiveMqServiceName, ICanCreateClientOrServer 
        where TServiceMessage : IMessage
    {
        private readonly ICanAddEndpoint _canAddEndpoint;
        private string _serviceName;
        private string _brokerUri;
        private string _assemblyNameContains = string.Empty;

        public ActiveMqFluentConfig(ICanAddEndpoint canAddEndpoint)
        {
            _canAddEndpoint = canAddEndpoint;
        }

        public ICanSpecifyActiveMqBroker Named(string serviceName)
        {
            _serviceName = serviceName;
            return this;
        }

        public ICanCreateClientOrServer FilterMessageTypeAssemblies(string assemblyNameContains)
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

        private ActiveMqServiceEndpointProvider<TServiceMessage> CreateProvider()
        {
            return new ActiveMqServiceEndpointProvider<TServiceMessage>(_serviceName, _brokerUri, _assemblyNameContains);
        }

        public ICanCreateClientOrServer UsingBroker(string brokerUri)
        {
            _brokerUri = brokerUri;
            return this;
        }
    }
}