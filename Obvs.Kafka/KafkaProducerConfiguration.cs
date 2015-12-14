namespace Obvs.Kafka
{
    public class KafkaProducerConfiguration
    {
        public int BatchFlushSize { get; set; } = 1000;
    }
}