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
            return new ServiceBus(GetEndpointClients(), GetEndpoints(), GetRequestCorrelationProvider());
        }

        public IServiceBusClient CreateClient()
        {
            return new ServiceBusClient(GetEndpointClients(), GetRequestCorrelationProvider());
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

        public ICanCreate UsingConsoleLogging(Func<IEndpoint, bool> enableLogging = null)
        {
            return UsingLogging(new ConsoleLoggerFactory(), enableLogging);
        }

        public ICanAddEndpointOrLoggingOrCorrelationOrCreate CorrelatesRequestWith(IRequestCorrelationProvider requestCorrelationProvider)
        {
            if (requestCorrelationProvider == null)
            {
                throw new ArgumentNullException("requestCorrelationProvider");
            }
            
            _requestCorrelationProvider = requestCorrelationProvider;
            return this;
        }

        private IEnumerable<IServiceEndpointClient> GetEndpointClients()
        {
            return _loggerFactory == null ? _endpointClients : _endpointClients.Where(ep => _enableLogging(ep)).Select(ep => ep.CreateLoggingProxy(_loggerFactory));
        }

        private IEnumerable<IServiceEndpoint> GetEndpoints()
        {
            return _loggerFactory == null ? _endpoints : _endpoints.Where(ep => _enableLogging(ep)).Select(ep => ep.CreateLoggingProxy(_loggerFactory));
        }

        private IRequestCorrelationProvider GetRequestCorrelationProvider()
        {
            return _requestCorrelationProvider ?? new DefaultRequestCorrelationProvider();
        }
    }
}