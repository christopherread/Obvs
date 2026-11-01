using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using Obvs.Extensions;
using Obvs.Transports;
using Obvs.Types;

namespace Obvs
{
    /// <summary>The bus: merges its services' endpoints into one stream per kind of message, and sends each message to the endpoints that take it.</summary>
    internal sealed class ServiceBus : IServiceBus, IDisposable
    {
        private static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(30);

        private readonly IReadOnlyList<IServiceEndpointClient<IMessage, ICommand, IEvent, IRequest, IResponse>> _endpointClients;
        private readonly IReadOnlyList<IServiceEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse>> _endpoints;
        private readonly IRequestCorrelationProvider _requestCorrelationProvider;
        private readonly TimeSpan _requestTimeout;
        private readonly TimeProvider _timeProvider;
        private readonly Subject<Exception> _exceptions = new Subject<Exception>();
        private int _disposed;

        /// <summary>A bus over these endpoints.</summary>
        /// <param name="endpointClients">The client endpoints of the services the bus uses</param>
        /// <param name="endpoints">The server endpoints of the services the bus serves</param>
        /// <param name="requestCorrelationProvider">How responses are matched to requests, by request ID by default</param>
        /// <param name="requestTimeout">How long a request waits for its response unless it says otherwise</param>
        /// <param name="timeProvider">Measures how long requests have waited</param>
        internal ServiceBus(IEnumerable<IServiceEndpointClient<IMessage, ICommand, IEvent, IRequest, IResponse>> endpointClients,
            IEnumerable<IServiceEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse>> endpoints,
            IRequestCorrelationProvider requestCorrelationProvider = null,
            TimeSpan? requestTimeout = null,
            TimeProvider timeProvider = null)
        {
            _endpointClients = endpointClients.ToArray();
            _endpoints = endpoints.ToArray();
            _requestCorrelationProvider = requestCorrelationProvider ?? new DefaultRequestCorrelationProvider();
            _requestTimeout = requestTimeout ?? DefaultRequestTimeout;
            _timeProvider = timeProvider ?? TimeProvider.System;

            Events = _endpointClients
                .Select(endpointClient => endpointClient.EventsWithErrorHandling(_exceptions))
                .Merge()
                .PublishRefCountRetriable();

            Requests = _endpoints
                .Select(endpoint => endpoint.RequestsWithErrorHandling(_exceptions))
                .Merge()
                .PublishRefCountRetriable();

            Commands = _endpoints
                .Select(endpoint => endpoint.CommandsWithErrorHandling(_exceptions))
                .Merge()
                .PublishRefCountRetriable();
        }

        /// <inheritdoc />
        public IObservable<IEvent> Events { get; }

        /// <inheritdoc />
        public IObservable<IRequest> Requests { get; }

        /// <inheritdoc />
        public IObservable<ICommand> Commands { get; }

        /// <inheritdoc />
        public IObservable<Exception> Exceptions => _exceptions;

        /// <inheritdoc />
        public Task SendAsync(ICommand command, CancellationToken cancellationToken = default)
        {
            return SendAsync(command, ObvsReceivedMessage.NoHeaders, cancellationToken);
        }

        /// <inheritdoc />
        public Task SendAsync(ICommand command, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default)
        {
            if (headers == null)
            {
                throw new ArgumentNullException(nameof(headers));
            }
            cancellationToken.ThrowIfCancellationRequested();

            var exceptions = new List<Exception>();

            var tasks = EndpointClientsThatCanHandle(command)
                .Select(endpoint => CatchUnlessCancelled(() => endpoint.SendAsync(command, headers, cancellationToken), exceptions, cancellationToken, $"Error sending command to endpoint {endpoint.GetType().FullName}"))
                .ToArray();

            if (exceptions.Any())
            {
                cancellationToken.ThrowIfCancellationRequested();
                throw new AggregateException($"Error sending command {command}", exceptions);
            }

            if (tasks.Length == 0)
            {
                throw new InvalidOperationException(NoEndpointMessage(command));
            }

            return Task.WhenAll(tasks);
        }

        /// <inheritdoc />
        public Task SendAsync(IEnumerable<ICommand> commands, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var commandsResolved = commands.ToArray();

            if (commandsResolved.Length == 0)
            {
                return Task.FromResult(true);
            }

            var exceptions = new List<Exception>();

            var tasks = commandsResolved.Select(command => CatchUnlessCancelled(() => SendAsync(command, cancellationToken), exceptions, cancellationToken)).ToArray();

            if (exceptions.Any())
            {
                cancellationToken.ThrowIfCancellationRequested();

                Exception[] GetInnerExceptions(Exception e) =>
                    e is AggregateException aggregateException
                        ? aggregateException.InnerExceptions.ToArray()
                        : new[] { e };

                throw new AggregateException("Error sending commands", exceptions.SelectMany(GetInnerExceptions));
            }

            return Task.WhenAll(tasks);
        }

        /// <inheritdoc />
        public Task<TResponse> RequestAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
            where TResponse : class, IResponse
        {
            return RequestAsync(request, ObvsReceivedMessage.NoHeaders, _requestTimeout, cancellationToken);
        }

        /// <inheritdoc />
        public Task<TResponse> RequestAsync<TResponse>(IRequest<TResponse> request, TimeSpan timeout, CancellationToken cancellationToken = default)
            where TResponse : class, IResponse
        {
            return RequestAsync(request, ObvsReceivedMessage.NoHeaders, timeout, cancellationToken);
        }

        /// <inheritdoc />
        public Task<TResponse> RequestAsync<TResponse>(IRequest<TResponse> request, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default)
            where TResponse : class, IResponse
        {
            return RequestAsync(request, headers, _requestTimeout, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<TResponse> RequestAsync<TResponse>(IRequest<TResponse> request, IReadOnlyDictionary<string, string> headers, TimeSpan timeout, CancellationToken cancellationToken = default)
            where TResponse : class, IResponse
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }
            if (headers == null)
            {
                throw new ArgumentNullException(nameof(headers));
            }

            using (var waiting = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                // Both started before the first await, so the timeout runs from when the request was sent
                var response = GetResponses(request, headers).OfType<TResponse>().FirstAsync().ToTask(waiting.Token);
                var expiry = Delay(timeout, waiting.Token);

                var first = await Task.WhenAny(response, expiry).ConfigureAwait(false);
                // Stops listening for responses, or stops the timer, whichever is still running
                waiting.Cancel();
                cancellationToken.ThrowIfCancellationRequested();
                if (first == response)
                {
                    return await response.ConfigureAwait(false);
                }
            }

            throw new TimeoutException($"No {typeof(TResponse).Name} response to {request.GetType().Name} within {timeout}.");
        }

        /// <inheritdoc />
        public IObservable<IResponse> GetResponses(IRequest request)
        {
            return GetResponses(request, ObvsReceivedMessage.NoHeaders);
        }

        /// <inheritdoc />
        public IObservable<IResponse> GetResponses(IRequest request, IReadOnlyDictionary<string, string> headers)
        {
            if (headers == null)
            {
                throw new ArgumentNullException(nameof(headers));
            }
            _requestCorrelationProvider.SetRequestCorrelationIds(request);

            return EndpointClientsThatCanHandle(request)
                .Select(endpoint => endpoint.GetResponses(request, headers)
                .Where(response => _requestCorrelationProvider.AreCorrelated(request, response)))
                .Merge()
                .PublishRefCountRetriable();
        }

        /// <inheritdoc />
        public IAsyncEnumerable<TEvent> ReadEventsAsync<TEvent>(CancellationToken cancellationToken = default, int capacity = 1024)
            where TEvent : IEvent
        {
            return new ObservableAsyncEnumerable<TEvent>(Events.OfType<TEvent>(), capacity, cancellationToken);
        }

        /// <inheritdoc />
        public IAsyncEnumerable<TCommand> ReadCommandsAsync<TCommand>(CancellationToken cancellationToken = default, int capacity = 1024)
            where TCommand : ICommand
        {
            return new ObservableAsyncEnumerable<TCommand>(Commands.OfType<TCommand>(), capacity, cancellationToken);
        }

        /// <inheritdoc />
        public Task PublishAsync(IEvent ev, CancellationToken cancellationToken = default)
        {
            return PublishAsync(ev, ObvsReceivedMessage.NoHeaders, cancellationToken);
        }

        /// <inheritdoc />
        public Task PublishAsync(IEvent ev, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default)
        {
            if (headers == null)
            {
                throw new ArgumentNullException(nameof(headers));
            }
            cancellationToken.ThrowIfCancellationRequested();

            var exceptions = new List<Exception>();

            var tasks = EndpointsThatCanHandle(ev)
                .Select(endpoint => CatchUnlessCancelled(() => endpoint.PublishAsync(ev, headers, cancellationToken), exceptions, cancellationToken, $"Error publishing event to endpoint {endpoint.GetType().FullName}"))
                .ToArray();

            if (exceptions.Any())
            {
                cancellationToken.ThrowIfCancellationRequested();
                throw new AggregateException($"Error publishing event {ev}", exceptions);
            }

            if (tasks.Length == 0)
            {
                throw new InvalidOperationException(NoEndpointMessage(ev));
            }

            return Task.WhenAll(tasks);
        }

        /// <inheritdoc />
        public Task ReplyAsync(IRequest request, IResponse response, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _requestCorrelationProvider.SetCorrelationIds(request, response);

            var exceptions = new List<Exception>();

            var tasks = EndpointsThatCanHandle(response)
                .Select(endpoint => CatchUnlessCancelled(() => endpoint.ReplyAsync(request, response, ObvsReceivedMessage.NoHeaders, cancellationToken), exceptions, cancellationToken, $"Error sending response to endpoint {endpoint.GetType().FullName}"))
                .ToArray();

            if (exceptions.Any())
            {
                cancellationToken.ThrowIfCancellationRequested();
                throw new AggregateException($"Error replying to request {request} with response {response}", exceptions);
            }

            return Task.WhenAll(tasks);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            // Safe to call more than once: a container can track the same bus behind several registrations
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
            {
                return;
            }

            _exceptions.Dispose();
            foreach (var endpointClient in _endpointClients)
            {
                endpointClient.Dispose();
            }
            foreach (var endpoint in _endpoints)
            {
                endpoint.Dispose();
            }
        }

        private IEnumerable<IServiceEndpointClient<IMessage, ICommand, IEvent, IRequest, IResponse>> EndpointClientsThatCanHandle(IMessage message)
        {
            return _endpointClients.Where(endpoint => endpoint.CanHandle(message)).ToArray();
        }

        private IEnumerable<IServiceEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse>> EndpointsThatCanHandle(IMessage message)
        {
            return _endpoints.Where(endpoint => endpoint.CanHandle(message)).ToArray();
        }

        private Task Delay(TimeSpan delay, CancellationToken cancellationToken)
        {
#if NET8_0_OR_GREATER
            return Task.Delay(delay, _timeProvider, cancellationToken);
#else
            return _timeProvider.Delay(delay, cancellationToken);
#endif
        }

        private static string NoEndpointMessage(IMessage message)
        {
            var type = message.GetType();
            return $"No service on the bus takes {type.Name}: it doesn't implement the message interface of any service the bus has. " +
                   $"Add the service it belongs to with AddService<TServiceMessage>(...) inside AddObvs, where {type.Name} implements " +
                   "TServiceMessage, or make it implement the message interface of a service the bus already has.";
        }

        // Checks cancellationToken outside the catch, so a send to several endpoints stops at the next one once
        // cancelled, and the cancellation isn't collected as that endpoint's failure
        private static Task CatchUnlessCancelled(Func<Task> func, List<Exception> exceptions, CancellationToken cancellationToken, string message = null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return func();
            }
            catch (Exception exception)
            {
                exceptions.Add(message == null ? exception : new Exception(message, exception));
            }
            return Task.FromResult(false);
        }
    }
}
