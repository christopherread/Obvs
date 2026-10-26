using System;

namespace Obvs.NATS
{
    /// <summary>
    /// A service's NATS JetStream settings. Each service has its own, as named options keyed by the service's
    /// name: <c>IOptionsMonitor&lt;NatsJetStreamOptions&gt;.Get("orders")</c>.
    /// </summary>
    public sealed class NatsJetStreamOptions
    {
        /// <summary>The server to connect to, which must have JetStream enabled, for example <c>nats://localhost:4222</c>. Required.</summary>
        public string Url { get; set; }

        /// <summary>
        /// How many times a message is delivered to a handler that keeps failing before it's republished to the service's
        /// dead-letter stream. 10 by default. Applied to consumers as their <c>MaxDeliver</c>.
        /// </summary>
        public int MaxDeliveryAttempts { get; set; } = 10;

        /// <summary>
        /// How many messages each consumer delivers to this bus at once. 1 by default, so messages arrive in order. Raise it
        /// so handlers with a <c>MaxConcurrency</c> above 1 can run in parallel; order isn't kept then.
        /// </summary>
        public int MaxConcurrentMessages { get; set; } = 1;

        /// <summary>
        /// How long a delivered message waits to be acknowledged before JetStream delivers it again. Applied to consumers;
        /// JetStream's default of 30 seconds when not set.
        /// </summary>
        public TimeSpan? AckWait { get; set; }

        /// <summary>
        /// How long a running handler keeps telling JetStream its message is in progress, which restarts the message's
        /// <see cref="AckWait"/>. 5 minutes by default; a handler that runs longer loses the message, and it's delivered again.
        /// </summary>
        public TimeSpan MaxInProgressDuration { get; set; } = TimeSpan.FromMinutes(5);
    }
}
