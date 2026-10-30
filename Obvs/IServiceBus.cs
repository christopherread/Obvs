using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Obvs.Configuration;
using Obvs.Extensions;
using Obvs.Transports;
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
        /// <summary>Publishes <paramref name="ev"/> to every client of its service, with <paramref name="headers"/>.</summary>
        /// <param name="ev">The event</param>
        /// <param name="headers">Headers to send with it, which its handlers read from <c>MessageContext.Headers</c>. Obvs sets the trace-context headers itself</param>
        /// <param name="cancellationToken">Stops waiting for the broker</param>
        Task PublishAsync(TEvent ev, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default);
        /// <summary>Sends <paramref name="response"/> to whoever sent <paramref name="request"/>.</summary>
        /// <param name="request">The request being answered</param>
        /// <param name="response">The answer</param>
        /// <param name="cancellationToken">Stops waiting for the broker</param>
        Task ReplyAsync(TRequest request, TResponse response, CancellationToken cancellationToken = default);
    }

    /// <summary>The bus, for the bus's standard message interfaces.</summary>
    internal class ServiceBus : IServiceBus, IDisposable
    {
        private readonly IServiceBus<IMessage, ICommand, IEvent, IRequest, IResponse> _serviceBus;
        private int _disposed;

        /// <summary>A bus over these endpoints.</summary>
        /// <param name="endpointClients">The client endpoints of the services the bus uses</param>
        /// <param name="endpoints">The server endpoints of the services the bus serves</param>
        /// <param name="requestCorrelationProvider">How responses are matched to requests, by request ID by default</param>
        internal ServiceBus(IEnumerable<IServiceEndpointClient<IMessage, ICommand, IEvent, IRequest, IResponse>> endpointClients, 
            IEnumerable<IServiceEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse>> endpoints, 
            IRequestCorrelationProvider<IRequest, IResponse> requestCorrelationProvider = null) :
            this(new ServiceBus<IMessage, ICommand, IEvent, IRequest, IResponse>(endpointClients, endpoints, requestCorrelationProvider ?? new DefaultRequestCorrelationProvider()))
        {
        }

        /// <summary>Wraps a generic bus.</summary>
        /// <param name="serviceBus">The bus</param>
        internal ServiceBus(IServiceBus<IMessage, ICommand, IEvent, IRequest, IResponse> serviceBus)
        {
            _serviceBus = serviceBus;
        }

        /// <inheritdoc />
        public IObservable<IEvent> Events => _serviceBus.Events;

        /// <inheritdoc />
        public Task SendAsync(ICommand command, CancellationToken cancellationToken = default)
        {
            return _serviceBus.SendAsync(command, cancellationToken);
        }

        /// <inheritdoc />
        public Task SendAsync(ICommand command, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default)
        {
            return _serviceBus.SendAsync(command, headers, cancellationToken);
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
        public IObservable<IResponse> GetResponses(IRequest request, IReadOnlyDictionary<string, string> headers)
        {
            return _serviceBus.GetResponses(request, headers);
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
        public Task PublishAsync(IEvent ev, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default)
        {
            return _serviceBus.PublishAsync(ev, headers, cancellationToken);
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

    /// <summary>The bus, over the services' endpoints.</summary>
    internal class ServiceBus<TMessage, TCommand, TEvent, TRequest, TResponse> : 
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
        internal ServiceBus(IEnumerable<IServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse>> endpointClients, 
                          IEnumerable<IServiceEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse>> endpoints, 
                          IRequestCorrelationProvider<TRequest, TResponse> requestCorrelationProvider)
            : base(endpointClients, endpoints, requestCorrelationProvider)
        {
            _requestCorrelationProvider = requestCorrelationProvider;

            Requests = Endpoints
                .Select(endpoint => endpoint.RequestsWithErrorHandling(_exceptions))
                .Merge()
                .PublishRefCountRetriable();

            Commands = Endpoints
                .Select(endpoint => endpoint.CommandsWithErrorHandling(_exceptions))
                .Merge()
                .PublishRefCountRetriable();
        }

        /// <inheritdoc />
        public IObservable<TRequest> Requests { get; }

        /// <inheritdoc />
        public IObservable<TCommand> Commands { get; }

        /// <inheritdoc />
        public Task PublishAsync(TEvent ev, CancellationToken cancellationToken = default)
        {
            return PublishAsync(ev, ObvsReceivedMessage.NoHeaders, cancellationToken);
        }

        /// <inheritdoc />
        public Task PublishAsync(TEvent ev, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default)
        {
            if (headers == null)
            {
                throw new ArgumentNullException(nameof(headers));
            }
            cancellationToken.ThrowIfCancellationRequested();

            var exceptions = new List<Exception>();

            var tasks = EndpointsThatCanHandle(ev)
                .Select(endpoint => CatchUnlessCancelled(() => endpoint.PublishAsync(ev, headers, cancellationToken), exceptions, cancellationToken, EventErrorMessage(endpoint)))
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
            cancellationToken.ThrowIfCancellationRequested();

            _requestCorrelationProvider.SetCorrelationIds(request, response);

            var exceptions = new List<Exception>();

            var tasks = EndpointsThatCanHandle(response)
                    .Select(endpoint => CatchUnlessCancelled(() => endpoint.ReplyAsync(request, response, ObvsReceivedMessage.NoHeaders, cancellationToken), exceptions, cancellationToken, ReplyErrorMessage(endpoint)))
                    .ToArray();

            if (exceptions.Any())
            {
                cancellationToken.ThrowIfCancellationRequested();
                throw new AggregateException(ReplyErrorMessage(request, response), exceptions);
            }

            return Task.WhenAll(tasks);
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