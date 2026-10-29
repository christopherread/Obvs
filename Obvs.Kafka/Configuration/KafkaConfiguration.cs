using System;
using System.Linq;
using System.Net;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

// v6's Kafka transport building blocks, which UseKafka(...) is built on: its public members are exempt from the rule that every one is documented
#pragma warning disable CS1570, CS1572, CS1591

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

        // For errors the Kafka clients recover from by themselves, which don't end a subscription
        internal ILogger Logger { get; set; } = NullLogger.Instance;

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
