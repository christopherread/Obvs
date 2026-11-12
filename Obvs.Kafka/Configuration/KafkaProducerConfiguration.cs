namespace Obvs.Kafka.Configuration
{
    /// <summary>How a Kafka <see cref="MessagePublisher{TMessage}"/> batches the messages it produces.</summary>
    public class KafkaProducerConfiguration
    {
        /// <summary>The most messages the producer sends to the broker in one batch, as its <c>batch.num.messages</c>. 1000 by default.</summary>
        public int BatchFlushSize { get; set; } = 1000;
    }
}