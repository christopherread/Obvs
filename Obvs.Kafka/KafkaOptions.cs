namespace Obvs.Kafka
{
    /// <summary>
    /// A service's Kafka settings. Each service has its own, as named options keyed by the service's
    /// name: <c>IOptionsMonitor&lt;KafkaOptions&gt;.Get("orders")</c>.
    /// </summary>
    public sealed class KafkaOptions
    {
        /// <summary>The brokers to connect to, comma separated, for example <c>localhost:9092</c>. Required.</summary>
        public string BootstrapServers { get; set; }
    }
}
