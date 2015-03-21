using System.Collections.Generic;

namespace Obvs.Configuration
{
    public class ServiceBusFluentCreator : ICanAddEndpointOrCreate
    {
        private readonly IList<IServiceEndpointClient> _endpointClients = new List<IServiceEndpointClient>();
        private readonly IList<IServiceEndpoint> _endpoints = new List<IServiceEndpoint>();

        public ICanAddEndpointOrCreate WithEndpoints(IServiceEndpointProvider serviceEndpointProvider)
        {
            _endpointClients.Add(serviceEndpointProvider.CreateEndpointClient());
            _endpoints.Add(serviceEndpointProvider.CreateEndpoint());
            return this;
        }

        public ICanAddEndpointOrCreate WithClientEndpoints(IServiceEndpointProvider serviceEndpointProvider)
        {
            _endpointClients.Add(serviceEndpointProvider.CreateEndpointClient());
            return this;
        }

        public ICanAddEndpointOrCreate WithServerEndpoints(IServiceEndpointProvider serviceEndpointProvider)
        {
            _endpoints.Add(serviceEndpointProvider.CreateEndpoint());
            return this;
        }

        public IServiceBus Create()
        {
            return new ServiceBus(_endpointClients, _endpoints);
        }

        public IServiceBusClient CreateClient()
        {
            return new ServiceBusClient(_endpointClients);
        }

        public ICanAddEndpointOrCreate WithEndpoint(IServiceEndpointClient endpointClient)
        {
            _endpointClients.Add(endpointClient);

            return this;
        }

        public ICanAddEndpointOrCreate WithEndpoint(IServiceEndpoint endpoint)
        {
            _endpoints.Add(endpoint);

            return this;
        }
    }
}