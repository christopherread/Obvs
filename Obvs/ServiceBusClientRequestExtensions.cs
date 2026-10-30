using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using Obvs.Transports;
using Obvs.Types;

namespace Obvs
{
    /// <summary>Awaitable requests.</summary>
    public static class ServiceBusClientRequestExtensions
    {
        /// <summary>
        /// Sends a request and returns the first response of type <typeparamref name="TResponse"/>, the response the
        /// request declares with <see cref="IRequest{TResponse}"/>. Throws <see cref="TimeoutException"/> if none arrives
        /// within <paramref name="timeout"/>, measured on <paramref name="timeProvider"/> (the system clock by default;
        /// pass a fake one to test timeouts).
        /// If the request can't be sent, throws the transport's error straight away.
        /// </summary>
        public static Task<TResponse> RequestAsync<TResponse>(this IServiceBusClient client, IRequest<TResponse> request, TimeSpan timeout,
            CancellationToken cancellationToken = default, TimeProvider timeProvider = null)
            where TResponse : class, IResponse
        {
            return client.RequestAsync(request, ObvsReceivedMessage.NoHeaders, timeout, cancellationToken, timeProvider);
        }

        /// <summary>
        /// Sends a request with <paramref name="headers"/>, which its handler reads from <c>MessageContext.Headers</c>, and
        /// returns the first response of type <typeparamref name="TResponse"/>, as
        /// <see cref="RequestAsync{TResponse}(IServiceBusClient, IRequest{TResponse}, TimeSpan, CancellationToken, TimeProvider)"/> does.
        /// </summary>
        public static async Task<TResponse> RequestAsync<TResponse>(this IServiceBusClient client, IRequest<TResponse> request, IReadOnlyDictionary<string, string> headers, TimeSpan timeout,
            CancellationToken cancellationToken = default, TimeProvider timeProvider = null)
            where TResponse : class, IResponse
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }
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
                var response = client.GetResponses(request, headers).OfType<TResponse>().FirstAsync().ToTask(waiting.Token);
                var expiry = Delay(timeout, timeProvider ?? TimeProvider.System, waiting.Token);

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

        private static Task Delay(TimeSpan delay, TimeProvider timeProvider, CancellationToken cancellationToken)
        {
#if NET8_0_OR_GREATER
            return Task.Delay(delay, timeProvider, cancellationToken);
#else
            return timeProvider.Delay(delay, cancellationToken);
#endif
        }
    }
}
