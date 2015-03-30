using System.Collections.Generic;
using System.Linq;
using Obvs.Logging;

namespace Obvs.Configuration
{
    public class ServiceBusFluentCreator : ICanAddEndpointOrLoggingOrCreate
    {
        private readonly IList<IServiceEndpointClient> _endpointClients = new List<IServiceEndpointClient>();
        private readonly IList<IServiceEndpoint> _endpoints = new List<IServiceEndpoint>();
        private ILoggerFactory _loggerFactory;

        public ICanAddEndpointOrLoggingOrCreate WithEndpoints(IServiceEndpointProvider serviceEndpointProvider)
        {
            _endpointClients.Add(serviceEndpointProvider.CreateEndpointClient());
            _endpoints.Add(serviceEndpointProvider.CreateEndpoint());
            return this;
        }

        public ICanAddEndpointOrLoggingOrCreate WithClientEndpoints(IServiceEndpointProvider serviceEndpointProvider)
        {
            _endpointClients.Add(serviceEndpointProvider.CreateEndpointClient());
            return this;
        }

        public ICanAddEndpointOrLoggingOrCreate WithServerEndpoints(IServiceEndpointProvider serviceEndpointProvider)
        {
            _endpoints.Add(serviceEndpointProvider.CreateEndpoint());
            return this;
        }

        public IServiceBus Create()
        {
            return _loggerFactory == null
                ? new ServiceBus(_endpointClients, _endpoints)
                : new ServiceBus(
                    _endpointClients.Select(ep => ep.CreateLoggingProxy(_loggerFactory)),
                    _endpoints.Select(ep => ep.CreateLoggingProxy(_loggerFactory)));
        }

        public IServiceBusClient CreateClient()
        {
            return _loggerFactory == null
                ? new ServiceBusClient(_endpointClients)
                : new ServiceBusClient(_endpointClients.Select(ep => ep.CreateLoggingProxy(_loggerFactory)));
        }

        public ICanAddEndpointOrLoggingOrCreate WithEndpoint(IServiceEndpointClient endpointClient)
        {
            _endpointClients.Add(endpointClient);
            return this;
        }

        public ICanAddEndpointOrLoggingOrCreate WithEndpoint(IServiceEndpoint endpoint)
        {
            _endpoints.Add(endpoint);
            return this;
        }

        public ICanCreate UsingLogging(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
            return this;
        }

        public ICanCreate UsingDebugLogging()
        {
            _loggerFactory = new DebugLoggerFactory();
            return this;
        }
    }
}