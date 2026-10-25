namespace Obvs.ActiveMQ
{
    /// <summary>
    /// A service's ActiveMQ settings. Each service has its own, as named options keyed by the service's
    /// name: <c>IOptionsMonitor&lt;ActiveMqOptions&gt;.Get("orders")</c>.
    /// </summary>
    public sealed class ActiveMqOptions
    {
        /// <summary>The broker to connect to, for example <c>activemq:tcp://localhost:61616</c>. Required.</summary>
        public string BrokerUri { get; set; }

        /// <summary>The user name to connect as, if the broker requires one.</summary>
        public string UserName { get; set; }

        /// <summary>The password for <see cref="UserName"/>.</summary>
        public string Password { get; set; }
    }
}
