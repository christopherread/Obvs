using System.Linq;
using System.Net;

namespace Obvs.Kafka.Configuration
{
    public class KafkaConfiguration
    {
        public KafkaConfiguration(params IPEndPoint[] bootstrapServers)
            : this(string.Join(",", bootstrapServers.Select(ip => ip.ToString())))
        {
        }

        public KafkaConfiguration(string bootstrapServers)
        {
            BootstrapServers = bootstrapServers;
        }

        public string BootstrapServers { get; }
    }
}