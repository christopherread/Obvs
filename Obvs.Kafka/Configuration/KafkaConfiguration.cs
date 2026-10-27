using System;
using System.Linq;
using System.Net;
using Confluent.Kafka;

namespace Obvs.Kafka.Configuration
{
    public class KafkaConfiguration
    {
        private readonly KafkaOptions _options;

        public KafkaConfiguration(params IPEndPoint[] bootstrapServers)
            : this(string.Join(",", bootstrapServers.Select(ip => ip.ToString())))
        {
        }

        public KafkaConfiguration(string bootstrapServers)
        {
            BootstrapServers = bootstrapServers;
        }

        // UseKafka's settings, security included
        internal KafkaConfiguration(KafkaOptions options)
            : this(options.BootstrapServers)
        {
            _options = options;
            var provider = options.OAuthBearerTokenProvider;
            if (provider != null)
            {
                OAuthBearerTokenRefreshHandler = (client, _) => KafkaOAuthBearer.Refresh(provider,
                    (value, lifetimeMs, principalName, extensions) => client.OAuthBearerSetToken(value, lifetimeMs, principalName, extensions),
                    client.OAuthBearerSetTokenFailure);
            }
        }

        public string BootstrapServers { get; }

        // Set on each client's builder when signing in with OAUTHBEARER
        internal Action<IClient, string> OAuthBearerTokenRefreshHandler { get; }

        // Sets the security settings on each client's config
        internal void ApplyTo(ClientConfig config)
        {
            if (_options != null)
            {
                KafkaSecurity.Apply(_options, config);
            }
        }
    }
}
