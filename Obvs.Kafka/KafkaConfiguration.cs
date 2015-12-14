using System.Linq;
using System.Net;

namespace Obvs.Kafka
{
    public class KafkaConfiguration
    {
        public KafkaConfiguration(params IPEndPoint[] seedAddresses)
            : this(string.Join(",", seedAddresses.Select(ip => ip.ToString())))
        {
        }

        public KafkaConfiguration(string connectionString)
        {
            SeedAddresses = connectionString;
        }

        public string SeedAddresses { get; }
    }
}