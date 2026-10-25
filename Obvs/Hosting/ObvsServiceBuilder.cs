using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Obvs.Configuration;
using Obvs.Serialization;
using Obvs.Types;

namespace Obvs.Hosting
{
    /// <summary>
    /// Configures one service on the bus: its transport, and whether this app is its client, its server or both.
    /// </summary>
    public sealed class ObvsServiceBuilder
    {
        private readonly ObvsBuilder _bus;
        private readonly List<Func<IServiceProvider, IEnumerable<string>>> _validators = new List<Func<IServiceProvider, IEnumerable<string>>>();

        internal ObvsServiceBuilder(string name, Type serviceMessageType, IServiceCollection services, ObvsBuilder bus)
        {
            Name = name;
            ServiceMessageType = serviceMessageType;
            Services = services;
            _bus = bus;
        }

        /// <summary>The service's name, unique on the bus.</summary>
        public string Name { get; }

        /// <summary>The interface every message of this service implements.</summary>
        public Type ServiceMessageType { get; }

        /// <summary>The container's services, for transports that need to register their own.</summary>
        public IServiceCollection Services { get; }

        /// <summary>
        /// The bus's instance ID, for transports to make broker-side names unique to this bus,
        /// so several buses in one process (as in tests) don't collide.
        /// </summary>
        public string InstanceId => _bus.InstanceId;

        /// <summary>The serializer set with <see cref="SerializedWith"/>, if any.</summary>
        public IMessageSerializer Serializer { get; private set; }

        /// <summary>The deserializer factory set with <see cref="SerializedWith"/>, if any.</summary>
        public IMessageDeserializerFactory DeserializerFactory { get; private set; }

        /// <summary>Serializes the service's messages with <paramref name="serializer"/> and <paramref name="deserializerFactory"/>.</summary>
        public ObvsServiceBuilder SerializedWith(IMessageSerializer serializer, IMessageDeserializerFactory deserializerFactory)
        {
            Serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            DeserializerFactory = deserializerFactory ?? throw new ArgumentNullException(nameof(deserializerFactory));
            return this;
        }

        /// <summary>
        /// Adds a check that runs when the bus starts. Return a message for each problem found, saying how to fix it;
        /// every problem from every service is reported together in one <see cref="ObvsConfigurationException"/>.
        /// </summary>
        public ObvsServiceBuilder ValidateWith(Func<IServiceProvider, IEnumerable<string>> validate)
        {
            _validators.Add(validate ?? throw new ArgumentNullException(nameof(validate)));
            return this;
        }

        internal IEnumerable<string> Validate(IServiceProvider serviceProvider)
        {
            return _validators.SelectMany(validate => validate(serviceProvider) ?? Enumerable.Empty<string>());
        }

        internal Func<IServiceProvider, IServiceEndpointProvider<IMessage, ICommand, IEvent, IRequest, IResponse>> EndpointProviderFactory { get; private set; }

        internal bool IsClient { get; private set; } = true;

        internal bool IsServer { get; private set; } = true;

        /// <summary>
        /// Uses an existing transport's endpoint provider, created from the container when the bus starts.
        /// Transports will get their own <c>Use&lt;Transport&gt;()</c> methods; this works with any of them meanwhile.
        /// </summary>
        public ObvsServiceBuilder UseEndpointProvider(Func<IServiceProvider, IServiceEndpointProvider<IMessage, ICommand, IEvent, IRequest, IResponse>> factory)
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
