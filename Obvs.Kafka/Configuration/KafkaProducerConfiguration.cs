namespace Obvs.Kafka.Configuration
{
    public class KafkaProducerConfiguration
    {
        public int BatchFlushSize { get; set; } = 1000;
    }
}