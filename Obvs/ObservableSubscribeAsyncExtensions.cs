using System;
using System.Threading;
using System.Threading.Tasks;
using Obvs.Hosting;

namespace Obvs
{
    /// <summary>
    /// Processes a stream's messages with async code, such as
    /// <c>bus.Commands.OfType&lt;PlaceOrder&gt;().SubscribeAsync(async (command, ct) =&gt; ...)</c>, in place of
    /// <c>Subscribe(async ...)</c>, which runs as async void.
    /// </summary>
    public static class ObservableSubscribeAsyncExtensions
    {
        /// <summary>
        /// Awaits <paramref name="onNext"/> for each message, one at a time in the order they arrive, or up to
        /// <paramref name="maxConcurrency"/> at once. A failure doesn't end the subscription: it's passed to
        /// <paramref name="onError"/>, and for a message from the bus it's also logged and recorded on the message's span.
        /// <para>
        /// A message from the bus is settled once processed: completed if <paramref name="onNext"/> succeeds, and abandoned
        /// if it throws, so a transport that keeps messages until they're settled, such as Azure Service Bus, SQS or
        /// JetStream, delivers it again. When several handlers or subscriptions process one message, it's settled once
        /// they all have. That holds for messages that reach SubscribeAsync while the bus delivers them, through operators
        /// such as <c>OfType</c> and <c>Where</c>; an operator that passes them on later, such as <c>ObserveOn</c>, or
        /// replaces them, such as <c>Select</c>, leaves them completed on arrival, as plain <c>Subscribe</c> does.
        /// </para>
        /// </summary>
        /// <param name="messages">The stream to process, such as one of the bus's, filtered with Rx operators.</param>
        /// <param name="onNext">Processes a message. Its token is cancelled when the subscription is disposed.</param>
        /// <param name="maxConcurrency">How many messages to process at once. With 1, the default, they're processed in order.</param>
        /// <param name="onError">Called with each message <paramref name="onNext"/> failed on, and the exception.</param>
        /// <returns>
        /// The subscription. Disposing it stops taking messages, cancels those being processed, and gives back to their
        /// transport any not yet processed. If the stream itself fails, processing stops.
        /// </returns>
        public static IDisposable SubscribeAsync<T>(this IObservable<T> messages, Func<T, CancellationToken, Task> onNext,
            int maxConcurrency = 1, Action<T, Exception> onError = null)
        {
            if (messages == null)
            {
                throw new ArgumentNullException(nameof(messages));
            }
            if (onNext == null)
            {
                throw new ArgumentNullException(nameof(onNext));
            }
            if (maxConcurrency < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxConcurrency), maxConcurrency, "SubscribeAsync needs to process at least one message at a time.");
            }

            return new MessageProcessor<T>(messages, onNext, maxConcurrency, onError);
        }

        /// <summary>
        /// Awaits <paramref name="onNext"/> for each message, as
        /// <see cref="SubscribeAsync{T}(IObservable{T}, Func{T, CancellationToken, Task}, int, Action{T, Exception})"/> does,
        /// for code that doesn't take a cancellation token.
        /// </summary>
        /// <param name="messages">The stream to process, such as one of the bus's, filtered with Rx operators.</param>
        /// <param name="onNext">Processes a message.</param>
        /// <param name="maxConcurrency">How many messages to process at once. With 1, the default, they're processed in order.</param>
        /// <param name="onError">Called with each message <paramref name="onNext"/> failed on, and the exception.</param>
        /// <returns>The subscription. Dispose it to stop processing.</returns>
        public static IDisposable SubscribeAsync<T>(this IObservable<T> messages, Func<T, Task> onNext,
            int maxConcurrency = 1, Action<T, Exception> onError = null)
        {
            if (onNext == null)
            {
                throw new ArgumentNullException(nameof(onNext));
            }

            return messages.SubscribeAsync((message, cancellationToken) => onNext(message), maxConcurrency, onError);
        }
    }
}
