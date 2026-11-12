using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Obvs.RabbitMQ
{
    /// <summary>
    /// A service's RabbitMQ settings. Each service has its own, as named options keyed by the service's
    /// name: <c>IOptionsMonitor&lt;RabbitMqOptions&gt;.Get("orders")</c>.
    /// </summary>
    public sealed class RabbitMqOptions
    {
        /// <summary>The broker to connect to, for example <c>amqp://user:password@localhost:5672/vhost</c>. Required.</summary>
        public Uri Uri { get; set; }

        /// <summary>Whether the connection reconnects and restores its channels after a network failure. True by default.</summary>
        public bool AutomaticRecoveryEnabled { get; set; } = true;

        /// <summary>
        /// How many times a command or request is delivered to a handler that keeps failing before it's dead-lettered to the
        /// service's <c>DeadLetters</c> queue. 10 by default. Applied to the service's queues as their <c>x-delivery-limit</c>,
        /// so changing it means deleting the queues for the bus to declare them again.
        /// </summary>
        public int MaxDeliveryAttempts { get; set; } = 10;

        /// <summary>
        /// How many commands, and how many requests, this bus takes from the service's queues at once, as its channel's
        /// prefetch count. When not set, as many as the service's handlers of them take at once: the sum of their
        /// <c>MaxConcurrency</c>, or 1 if no handler takes them. Messages arrive in order only one at a time. Set it to cap
        /// the handlers, or for messages processed with <c>SubscribeAsync</c> or Rx, which the bus can't see at startup.
        /// </summary>
        public int? MaxConcurrentMessages { get; set; }

        // Set by UseRabbitMq from the app's logging
        internal ILogger Logger { get; set; } = NullLogger.Instance;
    }
}
