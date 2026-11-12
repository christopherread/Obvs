using System;
using System.Collections.Generic;
using System.Linq;
using Obvs.Hosting;
using Obvs.Serialization;

namespace Obvs.Transports
{
    /// <summary>
    /// What a transport builds a service's endpoints from, given to the callbacks passed to <c>UseTransport</c> when the
    /// bus starts, once the service's configuration is complete.
    /// </summary>
    public sealed class ObvsTransportContext
    {
        private readonly IReadOnlyCollection<HandlerRegistration> _handlers;

        internal ObvsTransportContext(string serviceName, Type serviceMessageType, IReadOnlyCollection<Type> messages, string instanceId,
            IMessageSerializer serializer, IMessageDeserializerFactory deserializerFactory, bool isClient, bool isServer, IServiceProvider serviceProvider,
            IReadOnlyCollection<HandlerRegistration> handlers)
        {
            _handlers = handlers;
            ServiceName = serviceName;
            ServiceMessageType = serviceMessageType;
            Messages = messages;
            InstanceId = instanceId;
            Serializer = serializer;
            DeserializerFactory = deserializerFactory;
            IsClient = isClient;
            IsServer = isServer;
            ServiceProvider = serviceProvider;
        }

        /// <summary>The service's name, unique on the bus. Transports use it to name destinations.</summary>
        public string ServiceName { get; }

        /// <summary>The interface every message of the service implements.</summary>
        public Type ServiceMessageType { get; }

        /// <summary>
        /// The message types the service registers with <c>AddMessages</c>, or none if it finds its messages by
        /// scanning assemblies.
        /// </summary>
        public IReadOnlyCollection<Type> Messages { get; }

        /// <summary>
        /// The bus's instance ID, for making broker-side names unique to this bus, so several buses in one process
        /// (as in tests) don't collide.
        /// </summary>
        public string InstanceId { get; }

        /// <summary>
        /// Writes the service's messages: the serializer set with <c>SerializedWith</c>, or System.Text.Json by default,
        /// gzipped if the service is <c>Compressed</c>.
        /// </summary>
        public IMessageSerializer Serializer { get; }

        /// <summary>
        /// Reads the service's messages: the deserializer factory set with <c>SerializedWith</c>, or System.Text.Json by
        /// default. If the service registers its messages, it creates deserializers for those types only.
        /// </summary>
        public IMessageDeserializerFactory DeserializerFactory { get; }

        /// <summary>Whether this app is a client of the service, sending its commands and requests and receiving its events.</summary>
        public bool IsClient { get; }

        /// <summary>Whether this app is the service's server, receiving its commands and requests and publishing its events.</summary>
        public bool IsServer { get; }

        /// <summary>The app's services, for the transport's options and anything else it resolves.</summary>
        public IServiceProvider ServiceProvider { get; }

        /// <summary>
        /// How many messages of one kind the service's handlers take at once: the sum of <c>MaxConcurrency</c> over its
        /// handlers of <paramref name="messageKind"/>, or 1 if none takes them. Each handler takes its messages in order,
        /// so a queue its handlers share needs this many at once to keep them all busy. Transports default to it for each
        /// queue or subscription, unless the app sets a number of its own.
        /// </summary>
        /// <param name="messageKind"><c>ICommand</c>, <c>IEvent</c>, <c>IRequest</c> or <c>IResponse</c></param>
        public int HandlerConcurrency(Type messageKind)
        {
            if (messageKind == null)
            {
                throw new ArgumentNullException(nameof(messageKind));
            }

            var handlers = _handlers.Where(handler => messageKind.IsAssignableFrom(handler.MessageType)).ToArray();
            return handlers.Length == 0 ? 1 : handlers.Sum(handler => handler.Options.MaxConcurrency);
        }
    }
}
