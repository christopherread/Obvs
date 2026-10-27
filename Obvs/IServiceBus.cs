using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Obvs.Configuration;
using Obvs.Extensions;
using Obvs.Types;

namespace Obvs
{
    /// <summary>The bus, for the bus's standard message interfaces: resolve it from the container after <c>AddObvs</c>.</summary>
    public interface IServiceBus : IServiceBusClient, IServiceBus<IMessage, ICommand, IEvent, IRequest, IResponse>
    {
    }

    /// <summary>
    /// The bus as a service's server and client: it receives commands and requests, and publishes events and replies, as
    /// well as everything a <see cref="IServiceBusClient{TMessage, TCommand, TEvent, TRequest, TResponse}"/> does.
    /// </summary>
    public interface IServiceBus<TMessage, TCommand, TEvent, TRequest, TResponse> : IServiceBusClient<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TMessage : class
        where TCommand : class, TMessage
        where TEvent : class, TMessage
        where TRequest : class, TMessage
        where TResponse : class, TMessage
    {
        /// <summary>The requests the bus receives, for the services it's the server of. Reply to each with <see cref="ReplyAsync"/>.</summary>
        IObservable<TRequest> Requests { get; }
        /// <summary>The commands the bus receives, for the services it's the server of.</summary>
        IObservable<TCommand> Commands { get; }

        /// <summary>Publishes <paramref name="ev"/> to every client of its service.</summary>
        /// <param name="ev">The event</param>
        /// <param name="cancellationToken">Stops waiting for the broker</param>
        Task PublishAsync(TEvent ev, CancellationToken cancellationToken = default);
        /// <summary>Sends <paramref name="response"/> to whoever sent <paramref name="request"/>.</summary>
        /// <param name="request">The request being answered</param>
        /// <param name="response">The answer</param>
        /// <param name="cancellationToken">Stops waiting for the broker</param>
        Task ReplyAsync(TRequest request, TResponse response, CancellationToken cancellationToken = default);
    }

    /// <summary>The bus, for the bus's standard message interfaces.</summary>
    public class ServiceBus : IServiceBus, IDisposable
    {
        private readonly IServiceBus<IMessage, ICommand, IEvent, IRequest, IResponse> _serviceBus;
        private int _disposed;

        // ReSharper disable once UnusedMember.Global
        /// <summary>A bus over these endpoints.</summary>
        /// <param name="endpointClients">The client endpoints of the services the bus uses</param>
        /// <param name="endpoints">The server endpoints of the services the bus serves</param>
        /// <param name="requestCorrelationProvider">How responses are matched to requests, by request ID by default</param>
        public ServiceBus(IEnumerable<IServiceEndpointClient<IMessage, ICommand, IEvent, IRequest, IResponse>> endpointClients, 
            IEnumerable<IServiceEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse>> endpoints, 
            IRequestCorrelationProvider<IRequest, IResponse> requestCorrelationProvider = null) :
            this(new ServiceBus<IMessage, ICommand, IEvent, IRequest, IResponse>(endpointClients, endpoints, requestCorrelationProvider ?? new DefaultRequestCorrelationProvider()))
        {
        }

        /// <summary>Wraps a generic bus.</summary>
        /// <param name="serviceBus">The bus</param>
        public ServiceBus(IServiceBus<IMessage, ICommand, IEvent, IRequest, IResponse> serviceBus)
        {
            _serviceBus = serviceBus;
        }

        // ReSharper disable once UnusedMember.Global
        /// <summary>Starts v6's fluent configuration.</summary>
        [Obsolete("Configure buses with services.AddObvs(obvs => obvs.AddService<TServiceMessage>(name, service => service.Use<Transport>(...))) instead, or ObvsBus.StartAsync(...) without a host. See https://github.com/christopherread/Obvs/blob/master/docs/migrating-from-v6.md.")]
        public static ICanAddEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse> Configure()
        {
            return new ServiceBusFluentCreator<IMessage, ICommand, IEvent, IRequest, IResponse>(new DefaultRequestCorrelationProvider());
        }

        /// <inheritdoc />
        public IObservable<IEvent> Events => _serviceBus.Events;

        /// <inheritdoc />
        public Task SendAsync(ICommand command, CancellationToken cancellationToken = default)
        {
            return _serviceBus.SendAsync(command, cancellationToken);
        }

        /// <inheritdoc />
        public Task SendAsync(IEnumerable<ICommand> commands, CancellationToken cancellationToken = default)
        {
            return _serviceBus.SendAsync(commands, cancellationToken);
        }

        /// <inheritdoc />
        public IObservable<IResponse> GetResponses(IRequest request)
        {
            return _serviceBus.GetResponses(request);
        }

        /// <inheritdoc />
        public IObservable<T> GetResponses<T>(IRequest request) where T : IResponse
        {
            return _serviceBus.GetResponses<T>(request);
        }

        /// <inheritdoc />
        public IObservable<T> GetResponse<T>(IRequest request) where T : IResponse
        {
            return _serviceBus.GetResponse<T>(request);
        }

        /// <inheritdoc />
        public IObservable<Exception> Exceptions => _serviceBus.Exceptions;

        /// <inheritdoc />
        public IObservable<IRequest> Requests => _serviceBus.Requests;

        /// <inheritdoc />
        public IObservable<ICommand> Commands => _serviceBus.Commands;

        /// <inheritdoc />
        public Task PublishAsync(IEvent ev, CancellationToken cancellationToken = default)
        {
            return _serviceBus.PublishAsync(ev, cancellationToken);
        }

        /// <inheritdoc />
        public Task ReplyAsync(IRequest request, IResponse response, CancellationToken cancellationToken = default)
        {
            return _serviceBus.ReplyAsync(request, response, cancellationToken);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            // Safe to call more than once: a container can track the same bus behind several registrations
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
            {
                return;
            }
            ((IDisposable)_serviceBus).Dispose();
        }
    }

    /// <summary>The bus, over the services' endpoints and an optional local bus.</summary>
    public class ServiceBus<TMessage, TCommand, TEvent, TRequest, TResponse> : 
        ServiceBusClient<TMessage, TCommand, TEvent, TRequest, TResponse>, IServiceBus<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TMessage : class
        where TCommand : class, TMessage
        where TEvent : class, TMessage
        where TRequest : class, TMessage
        where TResponse : class, TMessage
    {
        private readonly IRequestCorrelationProvider<TRequest, TResponse> _requestCorrelationProvider;

        /// <summary>A bus over these endpoints.</summary>
        /// <param name="endpointClients">The client endpoints of the services the bus uses</param>
        /// <param name="endpoints">The server endpoints of the services the bus serves</param>
        /// <param name="requestCorrelationProvider">How responses are matched to requests</param>
        /// <param name="localBus">An in-process bus that also carries messages, or null</param>
        /// <param name="localBusOption">Which messages go to the local bus</param>
        public ServiceBus(IEnumerable<IServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse>> endpointClients, 
                          IEnumerable<IServiceEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse>> endpoints, 
                          IRequestCorrelationProvider<TRequest, TResponse> requestCorrelationProvider, 
                          IServiceBus<TMessage, TCommand, TEvent, TRequest, TResponse> localBus = null, LocalBusOptions localBusOption = LocalBusOptions.MessagesWithNoEndpointClients)
            : base(endpointClients, endpoints, requestCorrelationProvider, localBus, localBusOption)
        {
            _requestCorrelationProvider = requestCorrelationProvider;

            Requests = Endpoints
                .Select(endpoint => endpoint.RequestsWithErrorHandling(_exceptions))
                .Merge()
                .Merge(GetLocalRequests())
                .PublishRefCountRetriable();

            Commands = Endpoints
                .Select(endpoint => endpoint.CommandsWithErrorHandling(_exceptions))
                .Merge()
                .Merge(GetLocalCommands())
                .PublishRefCountRetriable();
        }

        /// <inheritdoc />
        public IObservable<TRequest> Requests { get; }

        /// <inheritdoc />
        public IObservable<TCommand> Commands { get; }

        /// <inheritdoc />
        public Task PublishAsync(TEvent ev, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var exceptions = new List<Exception>();

            var tasks = EndpointsThatCanHandle(ev)
                .Select(endpoint => CatchUnlessCancelled(() => endpoint.PublishAsync(ev, cancellationToken), exceptions, cancellationToken, EventErrorMessage(endpoint)))
                .Union(PublishLocal(ev, exceptions, cancellationToken))
                .ToArray();

            if (exceptions.Any())
            {
                cancellationToken.ThrowIfCancellationRequested();
                throw new AggregateException(EventErrorMessage(ev), exceptions);
            }

            if (tasks.Length == 0)
            {
                throw new InvalidOperationException(NoEndpointMessage(ev));
            }

            return Task.WhenAll(tasks);
        }

        /// <inheritdoc />
        public Task ReplyAsync(TRequest request, TResponse response, CancellationToken cancellationToken = default)
        {
            if (_requestCorrelationProvider == null)
            {
                throw new InvalidOperationException("Please configure the ServiceBus with a IRequestCorrelationProvider using the fluent configuration extension method .CorrelatesRequestWith()");
            }

            cancellationToken.ThrowIfCancellationRequested();

            _requestCorrelationProvider.SetCorrelationIds(request, response);

            var exceptions = new List<Exception>();

            var tasks = EndpointsThatCanHandle(response)
                    .Select(endpoint => CatchUnlessCancelled(() => endpoint.ReplyAsync(request, response, cancellationToken), exceptions, cancellationToken, ReplyErrorMessage(endpoint)))
                    .Union(ReplyLocal(request, response, exceptions, cancellationToken))
                    .ToArray();

            if (exceptions.Any())
            {
                cancellationToken.ThrowIfCancellationRequested();
                throw new AggregateException(ReplyErrorMessage(request, response), exceptions);
            }

            return Task.WhenAll(tasks);
        }

        // ReSharper disable once UnusedMember.Global
        /// <summary>Starts v6's fluent configuration.</summary>
        [Obsolete("Configure buses with services.AddObvs(obvs => obvs.AddService<TServiceMessage>(name, service => service.Use<Transport>(...))) instead, or ObvsBus.StartAsync(...) without a host. See https://github.com/christopherread/Obvs/blob/master/docs/migrating-from-v6.md.")]
        public static ICanAddEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse> Configure()
        {
            return new ServiceBusFluentCreator<TMessage, TCommand, TEvent, TRequest, TResponse>();
        }

        private IEnumerable<IServiceEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse>> EndpointsThatCanHandle(TMessage message)
        {
            return Endpoints.Where(endpoint => endpoint.CanHandle(message)).ToArray();
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            base.Dispose();
            foreach (var endpoint in Endpoints)
            {
                endpoint.Dispose();
            }
        }
    }
}