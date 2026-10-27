using System;
using System.Threading;
using System.Threading.Tasks;

namespace Obvs.Kafka
{
    /// <summary>
    /// A service's Kafka settings. Each service has its own, as named options keyed by the service's
    /// name: <c>IOptionsMonitor&lt;KafkaOptions&gt;.Get("orders")</c>. The security settings apply to every
    /// Kafka client the service's bus creates: its producers, its consumers and the admin client that creates its topics.
    /// </summary>
    public sealed class KafkaOptions
    {
        /// <summary>The brokers to connect to, comma separated, for example <c>localhost:9092</c>. Required.</summary>
        public string BootstrapServers { get; set; }

        /// <summary>
        /// How to connect to the brokers: <c>PLAINTEXT</c> (the default), <c>SSL</c>, <c>SASL_PLAINTEXT</c> or <c>SASL_SSL</c>.
        /// Managed platforms such as Confluent Cloud, Amazon MSK and Azure Event Hubs use <c>SASL_SSL</c>, or <c>SSL</c>.
        /// </summary>
        public string SecurityProtocol { get; set; }

        /// <summary>
        /// How to sign in with SASL: <c>PLAIN</c>, <c>SCRAM-SHA-256</c> or <c>SCRAM-SHA-512</c> with <see cref="SaslUsername"/>
        /// and <see cref="SaslPassword"/>, or <c>OAUTHBEARER</c> with <see cref="OAuthBearerTokenProvider"/>.
        /// Needs a <see cref="SecurityProtocol"/> of <c>SASL_SSL</c> or <c>SASL_PLAINTEXT</c>.
        /// </summary>
        public string SaslMechanism { get; set; }

        /// <summary>The user name for <c>PLAIN</c> or <c>SCRAM</c> sign-in, for example a Confluent Cloud API key, or <c>$ConnectionString</c> for Event Hubs.</summary>
        public string SaslUsername { get; set; }

        /// <summary>The password for <c>PLAIN</c> or <c>SCRAM</c> sign-in, for example a Confluent Cloud API secret, or an Event Hubs connection string.</summary>
        public string SaslPassword { get; set; }

        /// <summary>
        /// A file or directory of CA certificates to verify the brokers' certificates with, or <c>probe</c> to look in the
        /// usual places. The operating system's trusted CAs when not set, which is right for the managed platforms.
        /// </summary>
        public string SslCaLocation { get; set; }

        /// <summary>CA certificates to verify the brokers' certificates with, in PEM format, as an alternative to <see cref="SslCaLocation"/>.</summary>
        public string SslCaPem { get; set; }

        /// <summary>This client's certificate in PEM format, for brokers that authenticate clients with TLS (mutual TLS), such as Amazon MSK on port 9094.</summary>
        public string SslCertificateLocation { get; set; }

        /// <summary>The private key for <see cref="SslCertificateLocation"/>, in PEM format.</summary>
        public string SslKeyLocation { get; set; }

        /// <summary>The password for <see cref="SslKeyLocation"/>, if the key is encrypted.</summary>
        public string SslKeyPassword { get; set; }

        /// <summary>Whether to verify the brokers' certificates. True by default; turn it off only for a local broker with a self-signed certificate.</summary>
        public bool? EnableSslCertificateVerification { get; set; }

        /// <summary>
        /// Gets a token for <c>OAUTHBEARER</c> sign-in, for example signed with AWS IAM for Amazon MSK, or from Microsoft
        /// Entra ID for Event Hubs. Called when each Kafka client starts, and again before each token expires.
        /// Set it in code; it isn't read from configuration.
        /// </summary>
        public Func<CancellationToken, Task<KafkaOAuthBearerToken>> OAuthBearerTokenProvider { get; set; }
    }
}
