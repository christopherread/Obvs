namespace Obvs.NATS
{
    /// <summary>
    /// A service's NATS settings. Each service has its own, as named options keyed by the service's
    /// name: <c>IOptionsMonitor&lt;NatsOptions&gt;.Get("orders")</c>.
    /// </summary>
    public sealed class NatsOptions
    {
        /// <summary>The server to connect to, for example <c>nats://localhost:4222</c>. Required.</summary>
        public string Url { get; set; }
    }
}
