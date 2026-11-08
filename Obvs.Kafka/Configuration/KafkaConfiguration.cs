using System;
using System.Linq;
using System.Net;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Obvs.Kafka.Configuration
{
    /// <summary>The Kafka cluster a <see cref="MessageSource{TMessage}"/> or <see cref="MessagePublisher{TMessage}"/> connects to.</summary>
    public class KafkaConfiguration
    {
        private readonly KafkaOptions _options;

        /// <summary>Connects to the cluster through these brokers.</summary>
        /// <param name="bootstrapServers">The brokers the clients first connect to</param>
        public KafkaConfiguration(params IPEndPoint[] bootstrapServers)
            : this(string.Join(",", bootstrapServers.Select(ip => ip.ToString())))
        {
        }

        /// <summary>Connects to the cluster through these brokers.</summary>
        /// <param name="bootstrapServers">The brokers the clients first connect to, separated by commas, for example <c>localhost:9092</c></param>
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

        /// <summary>The brokers the clients first connect to, separated by commas.</summary>
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
