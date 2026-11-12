using System.Collections.Generic;
using System.Linq;
using Confluent.Kafka;

namespace Obvs.Kafka.Configuration
{
    // Reads KafkaOptions' security settings into a Confluent client config. Names are accepted as Kafka spells them
    // (SASL_SSL, SCRAM-SHA-512), as the platforms' docs and generated client configs do, or as Confluent.Kafka's enums do.
    internal static class KafkaSecurity
    {
        private static readonly Dictionary<string, SecurityProtocol> Protocols = new Dictionary<string, SecurityProtocol>
        {
            [Normalize("PLAINTEXT")] = SecurityProtocol.Plaintext,
            [Normalize("SSL")] = SecurityProtocol.Ssl,
            [Normalize("SASL_PLAINTEXT")] = SecurityProtocol.SaslPlaintext,
            [Normalize("SASL_SSL")] = SecurityProtocol.SaslSsl,
        };

        private static readonly Dictionary<string, SaslMechanism> Mechanisms = new Dictionary<string, SaslMechanism>
        {
            [Normalize("GSSAPI")] = SaslMechanism.Gssapi,
            [Normalize("PLAIN")] = SaslMechanism.Plain,
            [Normalize("SCRAM-SHA-256")] = SaslMechanism.ScramSha256,
            [Normalize("SCRAM-SHA-512")] = SaslMechanism.ScramSha512,
            [Normalize("OAUTHBEARER")] = SaslMechanism.OAuthBearer,
        };

        public static bool TryParseProtocol(string value, out SecurityProtocol protocol)
        {
            protocol = SecurityProtocol.Plaintext;
            return value != null && Protocols.TryGetValue(Normalize(value), out protocol);
        }

        public static bool TryParseMechanism(string value, out SaslMechanism mechanism)
        {
            mechanism = SaslMechanism.Gssapi;
            return value != null && Mechanisms.TryGetValue(Normalize(value), out mechanism);
        }

        public static bool IsSasl(SecurityProtocol protocol) => protocol == SecurityProtocol.SaslPlaintext || protocol == SecurityProtocol.SaslSsl;

        public static bool IsTls(SecurityProtocol protocol) => protocol == SecurityProtocol.Ssl || protocol == SecurityProtocol.SaslSsl;

        // Kafka's name for a mechanism, as errors show it
        public static string Name(SaslMechanism mechanism)
        {
            switch (mechanism)
            {
                case SaslMechanism.Plain: return "PLAIN";
                case SaslMechanism.ScramSha256: return "SCRAM-SHA-256";
                case SaslMechanism.ScramSha512: return "SCRAM-SHA-512";
                case SaslMechanism.OAuthBearer: return "OAUTHBEARER";
                default: return "GSSAPI";
            }
        }

        // Settings that aren't set are left to librdkafka's defaults, so a client without them is configured as before
        public static void Apply(KafkaOptions options, ClientConfig config)
        {
            if (TryParseProtocol(options.SecurityProtocol, out var protocol))
            {
                config.SecurityProtocol = protocol;
            }
            if (TryParseMechanism(options.SaslMechanism, out var mechanism))
            {
                config.SaslMechanism = mechanism;
            }
            if (!string.IsNullOrEmpty(options.SaslUsername))
            {
                config.SaslUsername = options.SaslUsername;
            }
            if (!string.IsNullOrEmpty(options.SaslPassword))
            {
                config.SaslPassword = options.SaslPassword;
            }
            if (!string.IsNullOrEmpty(options.SslCaLocation))
            {
                config.SslCaLocation = options.SslCaLocation;
            }
            if (!string.IsNullOrEmpty(options.SslCaPem))
            {
                config.SslCaPem = options.SslCaPem;
            }
            if (!string.IsNullOrEmpty(options.SslCertificateLocation))
            {
                config.SslCertificateLocation = options.SslCertificateLocation;
            }
            if (!string.IsNullOrEmpty(options.SslKeyLocation))
            {
                config.SslKeyLocation = options.SslKeyLocation;
            }
            if (!string.IsNullOrEmpty(options.SslKeyPassword))
            {
                config.SslKeyPassword = options.SslKeyPassword;
            }
            if (options.EnableSslCertificateVerification.HasValue)
            {
                config.EnableSslCertificateVerification = options.EnableSslCertificateVerification;
            }
        }

        private static string Normalize(string value)
        {
            return new string(value.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        }
    }
}
