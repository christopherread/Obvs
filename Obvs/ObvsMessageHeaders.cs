using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Obvs
{
    /// <summary>
    /// Headers that travel with a message without being part of it, such as trace context.
    /// Transports read a message's headers when sending it and set them on each message they receive.
    /// </summary>
    public static class ObvsMessageHeaders
    {
        /// <summary>The W3C trace-context header holding the sender's trace and span IDs.</summary>
        public const string TraceParent = "traceparent";

        /// <summary>The W3C trace-context header holding vendor-specific trace state.</summary>
        public const string TraceState = "tracestate";

        private static readonly IReadOnlyDictionary<string, string> None = new Dictionary<string, string>();

        // Keyed by the message instance, not its value, and weak, so headers go when their message does
        private static readonly ConditionalWeakTable<object, Holder> Headers = new ConditionalWeakTable<object, Holder>();

        /// <summary>The headers attached to <paramref name="message"/>, or an empty set if none are.</summary>
        public static IReadOnlyDictionary<string, string> Get(object message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            return Headers.TryGetValue(message, out var holder) ? holder.Headers : None;
        }

        /// <summary>Attaches <paramref name="headers"/> to <paramref name="message"/>, replacing any already attached.</summary>
        public static void Set(object message, IReadOnlyDictionary<string, string> headers)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }
            if (headers == null)
            {
                throw new ArgumentNullException(nameof(headers));
            }

            // A copy, so later changes to the caller's dictionary don't change what's sent
            var copy = new Dictionary<string, string>();
            foreach (var header in headers)
            {
                copy[header.Key] = header.Value;
            }
            Headers.GetValue(message, _ => new Holder()).Headers = copy;
        }

        // Mutable, because ConditionalWeakTable can't replace an entry's value on every target framework
        private sealed class Holder
        {
            public volatile IReadOnlyDictionary<string, string> Headers = None;
        }
    }
}
