using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Obvs.Configuration;
using Obvs.Serialization;
using Obvs.Serialization.SystemTextJson;
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
        private readonly List<HandlerRegistration> _handlers = new List<HandlerRegistration>();
        private readonly List<Type> _messages = new List<Type>();
        private IMessageDeserializerFactory _deserializerFactory = new SystemTextJsonMessageDeserializerFactory();

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

        /// <summary>The serializer set with <see cref="SerializedWith"/>, or System.Text.Json by default.</summary>
        public IMessageSerializer Serializer { get; private set; } = new SystemTextJsonMessageSerializer();

        /// <summary>
        /// The deserializer factory set with <see cref="SerializedWith"/>, or System.Text.Json by default. If the service registers its messages
        /// with <see cref="AddMessages"/>, it creates deserializers for those types only.
        /// </summary>
        public IMessageDeserializerFactory DeserializerFactory => _deserializerFactory == null || _messages.Count == 0
            ? _deserializerFactory
            : new RegisteredMessageDeserializerFactory(_deserializerFactory, _messages);

        /// <summary>Serializes the service's messages with <paramref name="serializer"/> and <paramref name="deserializerFactory"/>.</summary>
        public ObvsServiceBuilder SerializedWith(IMessageSerializer serializer, IMessageDeserializerFactory deserializerFactory)
        {
            Serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _deserializerFactory = deserializerFactory ?? throw new ArgumentNullException(nameof(deserializerFactory));
            return this;
        }

        /// <summary>
        /// The message types registered with <see cref="AddMessages"/>, or none if the service finds its messages by
        /// scanning assemblies.
        /// </summary>
        public IReadOnlyCollection<Type> Messages => _messages;

        /// <summary>
        /// Registers the service's message types: its commands, events, requests and responses. Its transport then
        /// deserializes exactly these, instead of scanning assemblies for types implementing the service's message
        /// interface. Each must be a public class implementing that interface and one of ICommand, IEvent, IRequest
        /// or IResponse, which is checked at startup. Can be called more than once.
        /// </summary>
        public ObvsServiceBuilder AddMessages(params Type[] messageTypes)
        {
            if (messageTypes == null || messageTypes.Contains(null))
            {
                throw new ArgumentNullException(nameof(messageTypes), "Message types can't be null.");
            }

            _messages.AddRange(messageTypes.Where(type => !_messages.Contains(type)).Distinct());
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
            return ValidateMessages()
                .Concat(ValidateHandlers())
                .Concat(_validators.SelectMany(validate => validate(serviceProvider) ?? Enumerable.Empty<string>()));
        }

        internal Func<IServiceProvider, IServiceEndpointProvider<IMessage, ICommand, IEvent, IRequest, IResponse>> EndpointProviderFactory { get; private set; }

        /// <summary>Whether this app is a client of the service, sending its commands and requests and receiving its events. True by default.</summary>
        public bool IsClient { get; private set; } = true;

        /// <summary>Whether this app is the service's server, receiving its commands and requests and publishing its events. True by default.</summary>
        public bool IsServer { get; private set; } = true;

        /// <summary>
        /// Uses an existing transport's endpoint provider, created from the container when the bus starts.
        /// Transports will get their own <c>Use&lt;Transport&gt;()</c> methods; this works with any of them meanwhile.
        /// </summary>
        public ObvsServiceBuilder UseEndpointProvider(Func<IServiceProvider, IServiceEndpointProvider<IMessage, ICommand, IEvent, IRequest, IResponse>> factory)
        {
            EndpointProviderFactory = factory ?? throw new ArgumentNullException(nameof(factory));
            return this;
        }

        /// <summary>
        /// Handles the service's <typeparamref name="TCommand"/> commands with <paramref name="handler"/>, which runs
        /// in its own DI scope per command. Handlers start with the host, and the host waits for running ones on stop.
        /// Exceptions are logged and don't stop later commands being handled. Requires this app to be the service's server.
        /// </summary>
        public ObvsServiceBuilder HandleCommand<TCommand>(Func<TCommand, MessageContext, CancellationToken, Task> handler, Action<HandlerOptions> configure = null)
            where TCommand : class, ICommand
        {
            return AddHandler(HandlerKind.Command, handler, configure, bus => bus.Commands.OfType<TCommand>());
        }

        /// <summary>Handles the service's <typeparamref name="TCommand"/> commands with a <typeparamref name="THandler"/> resolved per command.</summary>
        public ObvsServiceBuilder HandleCommand<TCommand, THandler>(Action<HandlerOptions> configure = null)
            where TCommand : class, ICommand
            where THandler : class, ICommandHandler<TCommand>
        {
            return HandleCommand<TCommand>((command, context, cancellationToken) =>
                ResolveHandler<THandler>(context).HandleAsync(command, context, cancellationToken), configure);
        }

        /// <summary>
        /// Handles the service's <typeparamref name="TEvent"/> events with <paramref name="handler"/>, in its own DI scope per event.
        /// Requires this app to be the service's client.
        /// </summary>
        public ObvsServiceBuilder HandleEvent<TEvent>(Func<TEvent, MessageContext, CancellationToken, Task> handler, Action<HandlerOptions> configure = null)
            where TEvent : class, IEvent
        {
            return AddHandler(HandlerKind.Event, handler, configure, bus => bus.Events.OfType<TEvent>());
        }

        /// <summary>Handles the service's <typeparamref name="TEvent"/> events with a <typeparamref name="THandler"/> resolved per event.</summary>
        public ObvsServiceBuilder HandleEvent<TEvent, THandler>(Action<HandlerOptions> configure = null)
            where TEvent : class, IEvent
            where THandler : class, IEventHandler<TEvent>
        {
            return HandleEvent<TEvent>((ev, context, cancellationToken) =>
                ResolveHandler<THandler>(context).HandleAsync(ev, context, cancellationToken), configure);
        }

        /// <summary>
        /// Answers the service's <typeparamref name="TRequest"/> requests with the response <paramref name="handler"/> returns,
        /// in its own DI scope per request. Requires this app to be the service's server.
        /// </summary>
        public ObvsServiceBuilder HandleRequest<TRequest, TResponse>(Func<TRequest, MessageContext, CancellationToken, Task<TResponse>> handler, Action<HandlerOptions> configure = null)
            where TRequest : class, IRequest
            where TResponse : class, IResponse
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            return AddHandler<TRequest>(HandlerKind.Request, async (request, context, cancellationToken) =>
            {
                var response = await handler(request, context, cancellationToken).ConfigureAwait(false);
                if (response != null)
                {
                    await context.ReplyAsync(request, response, cancellationToken).ConfigureAwait(false);
                }
            }, configure, bus => bus.Requests.OfType<TRequest>());
        }

        /// <summary>Answers the service's <typeparamref name="TRequest"/> requests with a <typeparamref name="THandler"/> resolved per request.</summary>
        public ObvsServiceBuilder HandleRequest<TRequest, TResponse, THandler>(Action<HandlerOptions> configure = null)
            where TRequest : class, IRequest
            where TResponse : class, IResponse
            where THandler : class, IRequestHandler<TRequest, TResponse>
        {
            return HandleRequest<TRequest, TResponse>((request, context, cancellationToken) =>
                ResolveHandler<THandler>(context).HandleAsync(request, context, cancellationToken), configure);
        }

        internal IReadOnlyList<HandlerRegistration> Handlers => _handlers;

        private ObvsServiceBuilder AddHandler<TMessage>(HandlerKind kind, Func<TMessage, MessageContext, CancellationToken, Task> handler,
            Action<HandlerOptions> configure, Func<IServiceBus, IObservable<TMessage>> messages)
            where TMessage : class, IMessage
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            var options = new HandlerOptions();
            configure?.Invoke(options);
            _handlers.Add(new HandlerRegistration(kind, typeof(TMessage), options, (bus, serviceName, serviceProvider, logger) =>
                HandlerRunner.Start(messages(bus), handler, options, bus, serviceName, serviceProvider, logger)));
            return this;
        }

        // A registered handler is used as is; otherwise one is created from the message's scope
        private static THandler ResolveHandler<THandler>(MessageContext context) where THandler : class
        {
            return ActivatorUtilities.GetServiceOrCreateInstance<THandler>(context.Services);
        }

        private static readonly (Type Interface, string Kind)[] MessageKinds =
        {
            (typeof(ICommand), "command"), (typeof(IEvent), "event"), (typeof(IRequest), "request"), (typeof(IResponse), "response"),
        };

        private IEnumerable<string> ValidateMessages()
        {
            foreach (var type in _messages)
            {
                if (!ServiceMessageType.IsAssignableFrom(type))
                {
                    yield return $"Service '{Name}' registers {type.Name}, which doesn't implement {ServiceMessageType.Name}. " +
                                 $"Make it implement {ServiceMessageType.Name}, or register it on the service it belongs to.";
                }

                var kinds = MessageKinds.Where(kind => kind.Interface.IsAssignableFrom(type)).Select(kind => kind.Kind).ToArray();
                if (kinds.Length != 1)
                {
                    var found = kinds.Length == 0 ? "isn't a command, event, request or response" : "is a " + string.Join(" and a ", kinds);
                    yield return $"Service '{Name}' registers {type.Name}, which {found}. " +
                                 "A message must implement exactly one of ICommand, IEvent, IRequest or IResponse.";
                }

                if (!type.IsClass || type.IsAbstract)
                {
                    yield return $"Service '{Name}' registers {type.Name}, which is {(type.IsInterface ? "an interface" : "abstract")}, so it can't be deserialized. " +
                                 "Register the concrete message classes instead.";
                }
                else if (!type.IsVisible)
                {
                    yield return $"Service '{Name}' registers {type.Name}, which isn't public, so it can't be deserialized. Make it public.";
                }
            }
        }

        private IEnumerable<string> ValidateHandlers()
        {
            foreach (var handler in _handlers)
            {
                var messageName = handler.MessageType.Name;
                if (!ServiceMessageType.IsAssignableFrom(handler.MessageType))
                {
                    yield return $"Service '{Name}' has a handler for {messageName}, which doesn't implement {ServiceMessageType.Name}. " +
                                 $"Register the handler on the service that {messageName} belongs to.";
                }
                if (handler.Kind != HandlerKind.Event && !IsServer)
                {
                    var kind = handler.Kind == HandlerKind.Command ? "commands" : "requests";
                    yield return $"Service '{Name}' handles {messageName} {kind} but is configured AsClient(), and only servers receive {kind}. " +
                                 "Call AsServer() or AsClientAndServer() instead.";
                }
                if (handler.Kind == HandlerKind.Event && !IsClient)
                {
                    yield return $"Service '{Name}' handles {messageName} events but is configured AsServer(), and only clients receive events. " +
                                 "Call AsClient() or AsClientAndServer() instead.";
                }
                if (handler.Options.MaxConcurrency < 1)
                {
                    yield return $"Service '{Name}' handles {messageName} with MaxConcurrency {handler.Options.MaxConcurrency}. It must be at least 1.";
                }
            }
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
