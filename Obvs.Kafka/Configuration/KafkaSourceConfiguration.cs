namespace Obvs.Kafka.Configuration
{
    public class KafkaSourceConfiguration
    {
        public int MinBytesPerFetch { get; set; } = 1;
        public int MaxBytesPerFetch { get; set; } = 262144;
    }
}