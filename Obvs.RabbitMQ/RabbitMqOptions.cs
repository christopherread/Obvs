using System;

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
    }
}
