// v6's Kafka transport building blocks, which UseKafka(...) is built on: its public members are exempt from the rule that every one is documented
#pragma warning disable CS1570, CS1572, CS1591

namespace Obvs.Kafka.Configuration
{
    public class KafkaSourceConfiguration
    {
        public int? FetchMinBytes { get; set; }
        public int? FetchMaxBytes { get; set; }
    }
}