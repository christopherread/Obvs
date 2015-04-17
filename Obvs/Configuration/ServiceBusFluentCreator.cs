using System;
using System.Collections.Generic;
using System.Linq;
using Obvs.Logging;

namespace Obvs.Configuration
{
    public class ServiceBusFluentCreator : ICanAddEndpointOrLoggingOrCorrelationOrCreate
    {
        private readonly IList<IServiceEndpointClient> _endpointClients = new List<IServiceEndpointClient>();
        private readonly IList<IServiceEndpoint> _endpoints = new List<IServiceEndpoint>();
        private ILoggerFactory _loggerFactory;
        private Func<IEndpoint, bool> _enableLogging;
        private IRequestCorrelationProvider _requestCorrelationProvider;

        public ICanAddEndpointOrLoggingOrCorrelationOrCreate WithEndpoints(IServiceEndpointProvider serviceEndpointProvider)
        {
            _endpointClients.Add(serviceEndpointProvider.CreateEndpointClient());
            _endpoints.Add(serviceEndpointProvider.CreateEndpoint());
            return this;
        }

        public ICanAddEndpointOrLoggingOrCorrelationOrCreate WithClientEndpoints(IServiceEndpointProvider serviceEndpointProvider)
        {
            _endpointClients.Add(serviceEndpointProvider.CreateEndpointClient());
            return this;
        }

        public ICanAddEndpointOrLoggingOrCorrelationOrCreate WithServerEndpoints(IServiceEndpointProvider serviceEndpointProvider)
        {
            _endpoints.Add(serviceEndpointProvider.CreateEndpoint());
            return this;
        }

        public IServiceBus Create()
        {
            IEnumerable<IServiceEndpointClient> endpointClients;
            IEnumerable<IServiceEndpoint> endpoints;

            GetPreparedEndpointsAndClients(out endpointClients, out endpoints);

            return new ServiceBus(endpointClients, endpoints, GetRequestCorrelationProvider());
        }

        public IServiceBusClient CreateClient()
        {
            IEnumerable<IServiceEndpointClient> endpointClients;
            IEnumerable<IServiceEndpoint> endpoints;

            GetPreparedEndpointsAndClients(out endpointClients, out endpoints);

            return new ServiceBusClient(_endpointClients, GetRequestCorrelationProvider());
        }

        public ICanAddEndpointOrLoggingOrCorrelationOrCreate WithEndpoint(IServiceEndpointClient endpointClient)
        {
            _endpointClients.Add(endpointClient);
            return this;
        }

        public ICanAddEndpointOrLoggingOrCorrelationOrCreate WithEndpoint(IServiceEndpoint endpoint)
        {
            _endpoints.Add(endpoint);
            return this;
        }

        public ICanCreate UsingLogging(ILoggerFactory loggerFactory, Func<IEndpoint, bool> enableLogging = null)
        {
            _enableLogging = enableLogging ?? (endpoint => true);
            _loggerFactory = loggerFactory;
            return this;
        }

        public ICanCreate UsingDebugLogging(Func<IEndpoint, bool> enableLogging = null)
        {
            return UsingLogging(new DebugLoggerFactory(), enableLogging);
        }

        public ICanAddEndpointOrLoggingOrCorrelationOrCreate CorrelatesRequestWith(IRequestCorrelationProvider requestCorrelationProvider)
        {
            if(requestCorrelationProvider == null) throw new ArgumentNullException("requestCorrelationProvider");
            
            _requestCorrelationProvider = requestCorrelationProvider;
            return this;
        }

        private void GetPreparedEndpointsAndClients(out IEnumerable<IServiceEndpointClient> endpointClients, out IEnumerable<IServiceEndpoint> endpoints)
        {
            if(_loggerFactory == null)
            {
                endpointClients = _endpointClients;
                endpoints = _endpoints;
            }
            else
            {
                endpointClients = _endpointClients.Where(ep => _enableLogging(ep)).Select(ep => ep.CreateLoggingProxy(_loggerFactory));
                endpoints = _endpoints.Where(ep => _enableLogging(ep)).Select(ep => ep.CreateLoggingProxy(_loggerFactory));
            }
        }

        private IRequestCorrelationProvider GetRequestCorrelationProvider()
        {
            return _requestCorrelationProvider ?? new DefaultRequestCorrelationProvider();
        }
    }
}