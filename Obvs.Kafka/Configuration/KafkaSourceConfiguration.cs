namespace Obvs.Kafka.Configuration
{
    /// <summary>How much a Kafka <see cref="MessageSource{TMessage}"/> fetches from the broker at once.</summary>
    public class KafkaSourceConfiguration
    {
        /// <summary>The least data the broker returns to a fetch, as the consumer's <c>fetch.min.bytes</c>. The client's default when null.</summary>
        public int? FetchMinBytes { get; set; }

        /// <summary>The most data the broker returns to a fetch, as the consumer's <c>fetch.max.bytes</c>. The client's default when null.</summary>
        public int? FetchMaxBytes { get; set; }
    }
}