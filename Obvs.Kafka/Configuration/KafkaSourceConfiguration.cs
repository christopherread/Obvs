namespace Obvs.Kafka.Configuration
{
    public class KafkaSourceConfiguration
    {
        public int? FetchMinBytes { get; set; }
        public int? FetchMaxBytes { get; set; }
    }
}