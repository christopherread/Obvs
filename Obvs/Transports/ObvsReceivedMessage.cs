using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Obvs.Transports
{
    /// <summary>
    /// How a transport settles a message it delivered, for transports whose broker keeps a message until
    /// it's settled. The handlers and <c>SubscribeAsync</c> subscriptions a message reaches settle it once
    /// they've all processed it; a message none of them processes is completed when it arrives.
    /// </summary>
    public interface IMessageSettlement
    {
        /// <summary>The message was handled: remove it from the broker.</summary>
        Task CompleteAsync();

        /// <summary>
        /// The message's handler threw <paramref name="exception"/>: have the broker deliver it again, or
        /// dead-letter it once the transport's delivery limit is reached.
        /// </summary>
        Task AbandonAsync(Exception exception);

        /// <summary>The bus is stopping before the message was handled: give it back to the broker to deliver again.</summary>
        Task ReleaseAsync();
    }

    /// <summary>
    /// Attaches what a transport received with a message, its headers and how to settle it, to the message it
    /// delivers, without it being part of the message. Apps read the headers with <see cref="ObvsMessageHeaders.Get"/>,
    /// and handlers with <c>MessageContext.Headers</c>.
    /// </summary>
    public static class ObvsReceivedMessage
    {
        internal static readonly IReadOnlyDictionary<string, string> NoHeaders = new Dictionary<string, string>();

        // Keyed by the message instance, not its value, and weak, so what's attached goes when its message does
        private static readonly ConditionalWeakTable<object, Holder> Received = new ConditionalWeakTable<object, Holder>();

        /// <summary>
        /// Attaches the <paramref name="headers"/> <paramref name="message"/> arrived with, and the
        /// <paramref name="settlement"/> that settles it, replacing anything already attached. Leave
        /// <paramref name="settlement"/> null if the transport settles the message on arrival.
        /// </summary>
        public static void Attach(object message, IReadOnlyDictionary<string, string> headers, IMessageSettlement settlement = null)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }
            if (headers == null)
            {
                throw new ArgumentNullException(nameof(headers));
            }

            Received.GetValue(message, _ => new Holder()).Value = new Attached(Copy(headers), settlement);
        }

        /// <summary>The settlement attached to <paramref name="message"/>, or null if its transport settles it on arrival.</summary>
        public static IMessageSettlement GetSettlement(object message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            return Received.TryGetValue(message, out var holder) ? holder.Value.Settlement : null;
        }

        internal static IReadOnlyDictionary<string, string> GetHeaders(object message)
        {
            return Received.TryGetValue(message, out var holder) ? holder.Value.Headers : NoHeaders;
        }

        // A copy, so later changes to the caller's dictionary don't change what was received
        internal static IReadOnlyDictionary<string, string> Copy(IReadOnlyDictionary<string, string> headers)
        {
            var copy = new Dictionary<string, string>();
            foreach (var header in headers)
            {
                copy[header.Key] = header.Value;
            }
            return copy;
        }

        private sealed class Attached
        {
            public Attached(IReadOnlyDictionary<string, string> headers, IMessageSettlement settlement)
            {
                Headers = headers;
                Settlement = settlement;
            }

            public IReadOnlyDictionary<string, string> Headers { get; }
            public IMessageSettlement Settlement { get; }
        }

        // Mutable, because ConditionalWeakTable can't replace an entry's value on every target framework
        private sealed class Holder
        {
            public volatile Attached Value = new Attached(NoHeaders, null);
        }
    }
}
