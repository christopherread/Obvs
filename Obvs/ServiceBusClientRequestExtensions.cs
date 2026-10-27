using System;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using Obvs.Types;

namespace Obvs
{
    /// <summary>Awaitable requests.</summary>
    public static class ServiceBusClientRequestExtensions
    {
        /// <summary>
        /// Sends a request and returns the first response of type <typeparamref name="TResponse"/>.
        /// Throws <see cref="TimeoutException"/> if none arrives within <paramref name="timeout"/>, measured on
        /// <paramref name="timeProvider"/> (the system clock by default; pass a fake one to test timeouts).
        /// </summary>
        public static async Task<TResponse> RequestAsync<TRequest, TResponse>(this IServiceBusClient client, TRequest request, TimeSpan timeout,
            CancellationToken cancellationToken = default, TimeProvider timeProvider = null)
            where TRequest : class, IRequest
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

            using (var waiting = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                // Both started before the first await, so the timeout runs from when the request was sent
                var response = client.GetResponses(request).OfType<TResponse>().FirstAsync().ToTask(waiting.Token);
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

            throw new TimeoutException($"No {typeof(TResponse).Name} response to {typeof(TRequest).Name} within {timeout}.");
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
