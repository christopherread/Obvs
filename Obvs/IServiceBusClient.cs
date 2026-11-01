using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Obvs.Types;

namespace Obvs
{
    /// <summary>
    /// The bus as a client of its services: it sends commands and requests, and receives events and responses.
    /// Resolve it from the container after <c>AddObvs</c>.
    /// </summary>
    public interface IServiceBusClient
    {
        /// <summary>The events the bus receives, from the services it's a client of.</summary>
        IObservable<IEvent> Events { get; }

        /// <summary>Sends <paramref name="command"/> to its service.</summary>
        /// <param name="command">The command</param>
        /// <param name="cancellationToken">Stops waiting for the broker</param>
        Task SendAsync(ICommand command, CancellationToken cancellationToken = default);

        /// <summary>Sends <paramref name="command"/> to its service, with <paramref name="headers"/>.</summary>
        /// <param name="command">The command</param>
        /// <param name="headers">Headers to send with it, which its handler reads from <c>MessageContext.Headers</c>. Obvs sets the trace-context headers itself</param>
        /// <param name="cancellationToken">Stops waiting for the broker</param>
        Task SendAsync(ICommand command, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default);

        /// <summary>Sends each of <paramref name="commands"/> to its service.</summary>
        /// <param name="commands">The commands</param>
        /// <param name="cancellationToken">Stops sending, and waiting for the broker</param>
        Task SendAsync(IEnumerable<ICommand> commands, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a request and returns the first response of type <typeparamref name="TResponse"/>, the response the
        /// request declares with <see cref="IRequest{TResponse}"/>. Throws <see cref="TimeoutException"/> if none arrives
        /// within the bus's request timeout: 30 seconds, unless set with <c>UseRequestTimeout</c>. If the request can't be
        /// sent, throws the transport's error straight away.
        /// </summary>
        /// <param name="request">The request</param>
        /// <param name="cancellationToken">Stops waiting for the response</param>
        Task<TResponse> RequestAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
            where TResponse : class, IResponse;

        /// <summary>
        /// Sends a request and returns the first response of type <typeparamref name="TResponse"/>, the response the
        /// request declares with <see cref="IRequest{TResponse}"/>. Throws <see cref="TimeoutException"/> if none arrives
        /// within <paramref name="timeout"/>. If the request can't be sent, throws the transport's error straight away.
        /// </summary>
        /// <param name="request">The request</param>
        /// <param name="timeout">How long to wait for the response</param>
        /// <param name="cancellationToken">Stops waiting for the response</param>
        Task<TResponse> RequestAsync<TResponse>(IRequest<TResponse> request, TimeSpan timeout, CancellationToken cancellationToken = default)
            where TResponse : class, IResponse;

        /// <summary>
        /// Sends a request with <paramref name="headers"/>, and returns the first response of type
        /// <typeparamref name="TResponse"/>, waiting for the bus's request timeout.
        /// </summary>
        /// <param name="request">The request</param>
        /// <param name="headers">Headers to send with it, which its handler reads from <c>MessageContext.Headers</c>. Obvs sets the trace-context headers itself</param>
        /// <param name="cancellationToken">Stops waiting for the response</param>
        Task<TResponse> RequestAsync<TResponse>(IRequest<TResponse> request, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default)
            where TResponse : class, IResponse;

        /// <summary>
        /// Sends a request with <paramref name="headers"/>, and returns the first response of type
        /// <typeparamref name="TResponse"/>, waiting for up to <paramref name="timeout"/>.
        /// </summary>
        /// <param name="request">The request</param>
        /// <param name="headers">Headers to send with it, which its handler reads from <c>MessageContext.Headers</c>. Obvs sets the trace-context headers itself</param>
        /// <param name="timeout">How long to wait for the response</param>
        /// <param name="cancellationToken">Stops waiting for the response</param>
        Task<TResponse> RequestAsync<TResponse>(IRequest<TResponse> request, IReadOnlyDictionary<string, string> headers, TimeSpan timeout, CancellationToken cancellationToken = default)
            where TResponse : class, IResponse;

        /// <summary>
        /// Sends <paramref name="request"/> when subscribed to, and returns its responses, for a request with several.
        /// For one response, use <c>RequestAsync</c>. If the send fails, the stream ends with its error.
        /// </summary>
        IObservable<IResponse> GetResponses(IRequest request);

        /// <summary>Sends <paramref name="request"/>, with <paramref name="headers"/>, when subscribed to, and returns its responses. If the send fails, the stream ends with its error.</summary>
        /// <param name="request">The request</param>
        /// <param name="headers">Headers to send with it, which its handler reads from <c>MessageContext.Headers</c>. Obvs sets the trace-context headers itself</param>
        IObservable<IResponse> GetResponses(IRequest request, IReadOnlyDictionary<string, string> headers);

        /// <summary>
        /// The bus's events of type <typeparamref name="TEvent"/>, read with <c>await foreach</c> from when enumeration
        /// starts until it's cancelled or stopped. Each enumeration subscribes on its own, and buffers up to
        /// <paramref name="capacity"/> events while its reader is busy. A reader that falls further behind gets an
        /// <see cref="InvalidOperationException"/>, rather than holding up the bus's other subscribers or missing events
        /// without knowing.
        /// </summary>
        /// <param name="cancellationToken">Stops reading</param>
        /// <param name="capacity">How many events the reader can fall behind by</param>
        IAsyncEnumerable<TEvent> ReadEventsAsync<TEvent>(CancellationToken cancellationToken = default, int capacity = 1024)
            where TEvent : IEvent;

        /// <summary>Errors the services' streams report while the bus runs, such as a queue that's gone. A hosted bus logs them too.</summary>
        IObservable<Exception> Exceptions { get; }
    }
}
