using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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
        private string _instanceId = Guid.NewGuid().ToString("N");

        internal ObvsBuilder(IServiceCollection services)
        {
            Services = services;
        }

        /// <summary>The container's services, for extensions that need to register their own.</summary>
        public IServiceCollection Services { get; }

        /// <summary>
        /// Identifies this bus to transports, which use it to keep broker-side names (such as queues) unique to the bus.
        /// A new GUID unless set with <see cref="UseInstanceId"/>.
        /// </summary>
        public string InstanceId => _instanceId;

        /// <summary>
        /// Sets the bus's instance ID, for example to keep queue names stable across restarts.
        /// It must be unique among buses connected to the same broker.
        /// </summary>
        public ObvsBuilder UseInstanceId(string instanceId)
        {
            if (string.IsNullOrWhiteSpace(instanceId))
            {
                throw new ArgumentException("An instance ID can't be blank: transports use it in broker-side names.", nameof(instanceId));
            }

            _instanceId = instanceId;
            return this;
        }

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

            var service = new ObvsServiceBuilder(name, typeof(TServiceMessage), Services, this);
            configure?.Invoke(service);
            _services.Add(service);
            return this;
        }

        internal IEnumerable<ObvsServiceBuilder> ServiceBuilders => _services;

        internal ServiceBus Build(IServiceProvider serviceProvider)
        {
            Validate(serviceProvider);

            var logger = serviceProvider.GetService<ILoggerFactory>()?.CreateLogger("Obvs.Hosting.Messages") ?? NullLogger.Instance;
            var endpoints = new List<IServiceEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse>>();
            var endpointClients = new List<IServiceEndpointClient<IMessage, ICommand, IEvent, IRequest, IResponse>>();
            foreach (var service in _services)
            {
                var provider = service.EndpointProviderFactory(serviceProvider);
                Func<Type, bool> isHandled = messageType => service.Handlers.Any(handler => handler.MessageType.IsAssignableFrom(messageType));
                if (service.IsServer)
                {
                    endpoints.Add(new TelemetryServiceEndpoint(new SettlingServiceEndpoint(provider.CreateEndpoint(), isHandled, service.Name, logger), service.Name, logger));
                }
                if (service.IsClient)
                {
                    endpointClients.Add(new TelemetryServiceEndpointClient(new SettlingServiceEndpointClient(provider.CreateEndpointClient(), isHandled, service.Name, logger), service.Name, logger));
                }
            }

            return new ServiceBus(endpointClients, endpoints);
        }

        private void Validate(IServiceProvider serviceProvider)
        {
            var errors = new List<string>();

            errors.AddRange(_services
                .GroupBy(service => service.Name)
                .Where(group => group.Count() > 1)
                .Select(group => $"Service name '{group.Key}' is used more than once. Each service on a bus needs a unique name."));

            errors.AddRange(_services
                .Where(service => service.EndpointProviderFactory == null)
                .Select(service => $"Service '{service.Name}' has no transport. Call UseEndpointProvider(...) on it inside AddService(\"{service.Name}\", ...)."));

            errors.AddRange(_services.SelectMany(service => service.Validate(serviceProvider)));

            if (errors.Count > 0)
            {
                throw new ObvsConfigurationException(errors);
            }
        }
    }
}
