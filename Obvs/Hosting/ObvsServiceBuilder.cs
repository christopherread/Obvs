using System;
using Microsoft.Extensions.DependencyInjection;
using Obvs.Configuration;

namespace Obvs.Hosting
{
    /// <summary>
    /// Configures one service on the bus: its transport, and whether this app is its client, its server or both.
    /// </summary>
    public sealed class ObvsServiceBuilder
    {
        internal ObvsServiceBuilder(string name, Type serviceMessageType, IServiceCollection services)
        {
            Name = name;
            ServiceMessageType = serviceMessageType;
            Services = services;
        }

        /// <summary>The service's name, unique on the bus.</summary>
        public string Name { get; }

        /// <summary>The interface every message of this service implements.</summary>
        public Type ServiceMessageType { get; }

        /// <summary>The container's services, for transports that need to register their own.</summary>
        public IServiceCollection Services { get; }

        internal Func<IServiceProvider, IServiceEndpointProvider> EndpointProviderFactory { get; private set; }

        internal bool IsClient { get; private set; } = true;

        internal bool IsServer { get; private set; } = true;

        /// <summary>
        /// Uses an existing transport's endpoint provider, created from the container when the bus starts.
        /// Transports will get their own <c>Use&lt;Transport&gt;()</c> methods; this works with any of them meanwhile.
        /// </summary>
        public ObvsServiceBuilder UseEndpointProvider(Func<IServiceProvider, IServiceEndpointProvider> factory)
        {
            EndpointProviderFactory = factory ?? throw new ArgumentNullException(nameof(factory));
            return this;
        }

        /// <summary>This app sends the service's commands and requests, and receives its events and responses.</summary>
        public ObvsServiceBuilder AsClient()
        {
            return As(client: true, server: false);
        }

        /// <summary>This app receives the service's commands and requests, and publishes its events and responses.</summary>
        public ObvsServiceBuilder AsServer()
        {
            return As(client: false, server: true);
        }

        /// <summary>This app is both client and server for the service. The default.</summary>
        public ObvsServiceBuilder AsClientAndServer()
        {
            return As(client: true, server: true);
        }

        private ObvsServiceBuilder As(bool client, bool server)
        {
            IsClient = client;
            IsServer = server;
            return this;
        }
    }
}
