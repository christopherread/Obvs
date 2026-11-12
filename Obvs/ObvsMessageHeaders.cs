using System;
using System.Collections.Generic;
using Obvs.Transports;

namespace Obvs
{
    /// <summary>
    /// Headers that travel with a message without being part of it, such as trace context. Pass them to
    /// <c>SendAsync</c>, <c>PublishAsync</c> or <c>RequestAsync</c>, and read the ones a message arrived with here,
    /// or in a handler from <c>MessageContext.Headers</c>.
    /// </summary>
    public static class ObvsMessageHeaders
    {
        /// <summary>The W3C trace-context header holding the sender's trace and span IDs. Obvs sets it on every message it sends.</summary>
        public const string TraceParent = "traceparent";

        /// <summary>The W3C trace-context header holding vendor-specific trace state. Obvs sets it on every message it sends.</summary>
        public const string TraceState = "tracestate";

        /// <summary>The headers <paramref name="message"/> arrived with, or an empty set if it wasn't received from a transport.</summary>
        public static IReadOnlyDictionary<string, string> Get(object message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            return ObvsReceivedMessage.GetHeaders(message);
        }
    }
}
