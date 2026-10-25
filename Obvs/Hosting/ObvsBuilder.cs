using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Obvs.Configuration;
using Obvs.Types;

namespace Obvs.Hosting
{
    /// <summary>
    /// Configures the services on a bus. Obtained from <c>services.AddObvs(...)</c>.
    /// </summary>
    public sealed class ObvsBuilder
    {
        private readonly List<ObvsServiceBuilder> _services = new List<ObvsServiceBuilder>();

        internal ObvsBuilder(IServiceCollection services)
        {
            Services = services;
        }

        /// <summary>The container's services, for extensions that need to register their own.</summary>
        public IServiceCollection Services { get; }

        /// <summary>
        /// Adds a service whose messages all implement <typeparamref name="TServiceMessage"/>.
        /// </summary>
        /// <param name="name">The service's name, unique on the bus. Transports use it to name destinations.</param>
        public ObvsBuilder AddService<TServiceMessage>(string name, Action<ObvsServiceBuilder> configure)
            where TServiceMessage : class, IMessage
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A service needs a name, which is used to name its destinations on the broker.", nameof(name));
            }

            var service = new ObvsServiceBuilder(name, typeof(TServiceMessage), Services);
            configure?.Invoke(service);
            _services.Add(service);
            return this;
        }

        internal ServiceBus Build(IServiceProvider serviceProvider)
        {
            Validate();

            var endpoints = new List<IServiceEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse>>();
            var endpointClients = new List<IServiceEndpointClient<IMessage, ICommand, IEvent, IRequest, IResponse>>();
            foreach (var service in _services)
            {
                var provider = service.EndpointProviderFactory(serviceProvider);
                if (service.IsServer)
                {
                    endpoints.Add(provider.CreateEndpoint());
                }
                if (service.IsClient)
                {
                    endpointClients.Add(provider.CreateEndpointClient());
                }
            }

            return new ServiceBus(endpointClients, endpoints);
        }

        private void Validate()
        {
            var errors = new List<string>();

            errors.AddRange(_services
                .GroupBy(service => service.Name)
                .Where(group => group.Count() > 1)
                .Select(group => $"Service name '{group.Key}' is used more than once. Each service on a bus needs a unique name."));

            errors.AddRange(_services
                .Where(service => service.EndpointProviderFactory == null)
                .Select(service => $"Service '{service.Name}' has no transport. Call UseEndpointProvider(...) on it inside AddService(\"{service.Name}\", ...)."));

            if (errors.Count > 0)
            {
                throw new ObvsConfigurationException(errors);
            }
        }
    }
}
