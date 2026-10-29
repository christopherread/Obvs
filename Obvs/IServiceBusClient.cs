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
    /// <summary>The bus as a client, for the bus's standard message interfaces: resolve it from the container after <c>AddObvs</c>.</summary>
    public interface IServiceBusClient : IServiceBusClient<IMessage, ICommand, IEvent, IRequest, IResponse>
    {
    }

    /// <summary>
    /// The bus as a client of its services: it sends commands and requests, and receives events and responses. Use
    /// <see cref="ServiceBusClientRequestExtensions.RequestAsync{TResponse}"/> to await a response.
    /// </summary>
    public interface IServiceBusClient<TMessage, in TCommand, out TEvent, in TRequest, TResponse>
        where TMessage : class
        where TCommand : class, TMessage
        where TEvent : class, TMessage
        where TRequest : class, TMessage
        where TResponse : class, TMessage
    {
        /// <summary>The events the bus receives, from the services it's a client of.</summary>
        IObservable<TEvent> Events { get; }

        /// <summary>Sends <paramref name="command"/> to its service.</summary>
        /// <param name="command">The command</param>
        /// <param name="cancellationToken">Stops waiting for the broker</param>
        Task SendAsync(TCommand command, CancellationToken cancellationToken = default);
        /// <summary>Sends each of <paramref name="commands"/> to its service.</summary>
        /// <param name="commands">The commands</param>
        /// <param name="cancellationToken">Stops sending, and waiting for the broker</param>
        Task SendAsync(IEnumerable<TCommand> commands, CancellationToken cancellationToken = default);

        /// <summary>Sends <paramref name="request"/> when subscribed to, and returns its responses. If the send fails, the stream ends with its error.</summary>
        IObservable<TResponse> GetResponses(TRequest request);
        /// <summary>Sends <paramref name="request"/> when subscribed to, and returns its responses of type <typeparamref name="T"/>.</summary>
        IObservable<T> GetResponses<T>(TRequest request) where T : TResponse;
        /// <summary>Sends <paramref name="request"/> when subscribed to, and returns its first response of type <typeparamref name="T"/>.</summary>
        IObservable<T> GetResponse<T>(TRequest request) where T : TResponse;

        /// <summary>Errors the services' streams report while the bus runs, such as a queue that's gone. A hosted bus logs them too.</summary>
        IObservable<Exception> Exceptions { get; }
    }

    /// <summary>The bus as a client, for the bus's standard message interfaces.</summary>
    public class ServiceBusClient : IServiceBusClient, IDisposable
    {
        private readonly IServiceBusClient<IMessage, ICommand, IEvent, IRequest, IResponse> _serviceBusClient;

        /// <summary>Wraps a generic client.</summary>
        /// <param name="serviceBusClient">The client</param>
        internal ServiceBusClient(IServiceBusClient<IMessage, ICommand, IEvent, IRequest, IResponse> serviceBusClient)
        {
            _serviceBusClient = serviceBusClient;
        }

        /// <inheritdoc />
        public IObservable<IEvent> Events => _serviceBusClient.Events;

        /// <inheritdoc />
        public Task SendAsync(ICommand command, CancellationToken cancellationToken = default)
        {
            return _serviceBusClient.SendAsync(command, cancellationToken);
        }

        /// <inheritdoc />
        public Task SendAsync(IEnumerable<ICommand> commands, CancellationToken cancellationToken = default)
        {
            return _serviceBusClient.SendAsync(commands, cancellationToken);
        }

        /// <inheritdoc />
        public IObservable<IResponse> GetResponses(IRequest request)
        {
            return _serviceBusClient.GetResponses(request);
        }

        /// <inheritdoc />
        public IObservable<T> GetResponses<T>(IRequest request) where T : IResponse
        {
            return _serviceBusClient.GetResponses<T>(request);
        }

        /// <inheritdoc />
        public IObservable<T> GetResponse<T>(IRequest request) where T : IResponse
        {
            return _serviceBusClient.GetResponse<T>(request);
        }

        /// <inheritdoc />
        public IObservable<Exception> Exceptions => _serviceBusClient.Exceptions;

        /// <inheritdoc />
        public void Dispose()
        {
            ((IDisposable)_serviceBusClient).Dispose();
        }
    }

    /// <summary> merges multiple configured <see cref="IServiceEndpoint"/>s and <see cref="IServiceEndpointClient"/>s into an Observable stream per message type </summary>
    public class ServiceBusClient<TMessage, TCommand, TEvent, TRequest, TResponse> : ServiceBusErrorHandlingBase<TMessage, TCommand, TEvent, TRequest, TResponse>
        , IServiceBusClient<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TMessage : class
        where TCommand : class, TMessage
        where TEvent : class, TMessage
        where TRequest : class, TMessage
        where TResponse : class, TMessage
    {
        /// <summary>The server endpoints of the services the bus serves.</summary>
        protected readonly IEnumerable<IServiceEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse>> Endpoints;
        private readonly IEnumerable<IServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse>> _endpointClients;
        private readonly IRequestCorrelationProvider<TRequest, TResponse> _requestCorrelationProvider;

        /// <summary>A client over these endpoints.</summary>
        /// <param name="endpointClients">The client endpoints of the services the bus uses</param>
        /// <param name="endpoints">The server endpoints of the services the bus serves</param>
        /// <param name="requestCorrelationProvider">How responses are matched to requests</param>
        internal ServiceBusClient(IEnumerable<IServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse>> endpointClients, 
                                IEnumerable<IServiceEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse>> endpoints, 
                                IRequestCorrelationProvider<TRequest, TResponse> requestCorrelationProvider)
        {
            Endpoints = endpoints.ToList();

            _endpointClients = endpointClients.ToArray();

            Events = _endpointClients
                .Select(endpointClient => endpointClient.EventsWithErrorHandling(_exceptions))
                .Merge()
                .PublishRefCountRetriable();

            _requestCorrelationProvider = requestCorrelationProvider ?? throw new ArgumentNullException(nameof(requestCorrelationProvider));
        }

        /// <inheritdoc />
        public IObservable<TEvent> Events { get; }

        /// <inheritdoc />
        public Task SendAsync(TCommand command, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var exceptions = new List<Exception>();

            var tasks = EndpointClientsThatCanHandle(command)
                .Select(endpoint => CatchUnlessCancelled(() => endpoint.SendAsync(command, cancellationToken), exceptions, cancellationToken, CommandErrorMessage(endpoint)))
                .ToArray();

            if (exceptions.Any())
            {
                cancellationToken.ThrowIfCancellationRequested();
                throw new AggregateException(CommandErrorMessage(command), exceptions);
            }

            if (tasks.Length == 0)
            {
                throw new InvalidOperationException(NoEndpointMessage(command));
            }

            return Task.WhenAll(tasks);
        }

        /// <inheritdoc />
        public Task SendAsync(IEnumerable<TCommand> commands, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var commandsResolved = commands.ToArray();

            if (commandsResolved.Length == 0)
                return Task.FromResult(true);

            var exceptions = new List<Exception>();
            
            var tasks = commandsResolved.Select(command => CatchUnlessCancelled(() => SendAsync(command, cancellationToken), exceptions, cancellationToken)).ToArray();

            if (exceptions.Any())
            {
                cancellationToken.ThrowIfCancellationRequested();

                Exception[] GetInnerExceptions(Exception e) =>
                    e is AggregateException aggregateException
                        ? aggregateException.InnerExceptions.ToArray()
                        : new[] {e};

                throw new AggregateException(CommandErrorMessage(), exceptions.SelectMany(GetInnerExceptions));
            }

            if (tasks.Length == 0)
            {
                throw new Exception("No endpoint configured for any of these commands, please check your ServiceBus configuration.");
            }

            return Task.WhenAll(tasks);
        }

        /// <inheritdoc />
        public IObservable<TResponse> GetResponses(TRequest request)
        {
            _requestCorrelationProvider.SetRequestCorrelationIds(request);

            return EndpointClientsThatCanHandle(request)
                .Select(endpoint => endpoint.GetResponses(request)
                .Where(response => _requestCorrelationProvider.AreCorrelated(request, response)))
                .Merge()
                .PublishRefCountRetriable();
        }

        /// <inheritdoc />
        public IObservable<T> GetResponses<T>(TRequest request) where T : TResponse
        {
            return GetResponses(request).OfType<T>();
        }

        /// <inheritdoc />
        public IObservable<T> GetResponse<T>(TRequest request) where T : TResponse
        {
            return GetResponses(request).OfType<T>().Take(1);
        }

        private IEnumerable<IServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse>> EndpointClientsThatCanHandle(TMessage message)
        {
            return _endpointClients.Where(endpoint => endpoint.CanHandle(message)).ToArray();
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            base.Dispose();
            foreach (var endpointClient in _endpointClients)
            {
                endpointClient.Dispose();
            }
        }
    }
}