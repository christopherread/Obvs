using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Confluent.Kafka;

namespace Obvs.Kafka.Configuration
{
    // Startup checks on KafkaOptions' security settings: values Kafka doesn't know, settings that contradict each other,
    // and settings the managed platforms are known to need. whereToSet names where a setting comes from, such as a
    // configuration key or options.X in UseKafka(...).
    internal static class KafkaSecurityChecks
    {
        private const string Mechanisms = "PLAIN, SCRAM-SHA-256, SCRAM-SHA-512 or OAUTHBEARER";
        private const string UserNameMechanisms = "PLAIN, SCRAM-SHA-256 or SCRAM-SHA-512";
        private const string SetTokenProvider = "Set options.OAuthBearerTokenProvider in UseKafka(...), or with settings from configuration, " +
                                                "in UseKafka(configuration, options => options.OAuthBearerTokenProvider = ...)";

        // Amazon MSK's ports, each for one kind of access control; public access adds 100
        private static readonly Dictionary<int, (string AccessControl, SecurityProtocol Protocol, SaslMechanism? Mechanism, string Fix)> MskPorts =
            new Dictionary<int, (string, SecurityProtocol, SaslMechanism?, string)>
            {
                [9094] = ("TLS", SecurityProtocol.Ssl, null,
                    "Set {SecurityProtocol} to SSL, without a SaslMechanism, and SslCertificateLocation and SslKeyLocation if the cluster authenticates clients by certificate"),
                [9096] = ("SASL/SCRAM", SecurityProtocol.SaslSsl, SaslMechanism.ScramSha512,
                    "Set {SecurityProtocol} to SASL_SSL and {SaslMechanism} to SCRAM-SHA-512 (MSK supports only SHA-512), with the user name and password from the cluster's secret in AWS Secrets Manager"),
                [9098] = ("IAM", SecurityProtocol.SaslSsl, SaslMechanism.OAuthBearer,
                    "Set {SecurityProtocol} to SASL_SSL and {SaslMechanism} to OAUTHBEARER, with an OAuthBearerTokenProvider that signs tokens with AWS IAM, for example with the AWS.MSK.Auth package"),
            };

        public static IEnumerable<string> Validate(string serviceName, KafkaOptions options, Func<string, string> whereToSet)
        {
            var service = $"Service '{serviceName}'";
            var hasProtocol = !string.IsNullOrWhiteSpace(options.SecurityProtocol);
            var hasMechanism = !string.IsNullOrWhiteSpace(options.SaslMechanism);
            var hasUsername = !string.IsNullOrEmpty(options.SaslUsername);
            var hasPassword = !string.IsNullOrEmpty(options.SaslPassword);
            var hasProvider = options.OAuthBearerTokenProvider != null;

            var protocol = SecurityProtocol.Plaintext;
            var protocolKnown = !hasProtocol || KafkaSecurity.TryParseProtocol(options.SecurityProtocol, out protocol);
            var protocolName = hasProtocol ? options.SecurityProtocol : "not set, so PLAINTEXT";
            if (!protocolKnown)
            {
                yield return $"{service} sets Kafka SecurityProtocol to '{options.SecurityProtocol}', which isn't a Kafka security protocol. " +
                             $"Set {whereToSet("SecurityProtocol")} to PLAINTEXT, SSL, SASL_PLAINTEXT or SASL_SSL.";
            }

            var mechanism = SaslMechanism.Gssapi;
            var mechanismKnown = hasMechanism && KafkaSecurity.TryParseMechanism(options.SaslMechanism, out mechanism);
            if (hasMechanism && !mechanismKnown)
            {
                var iam = options.SaslMechanism.IndexOf("IAM", StringComparison.OrdinalIgnoreCase) >= 0
                    ? " Amazon MSK's IAM sign-in is OAUTHBEARER, with an OAuthBearerTokenProvider that signs tokens with AWS IAM."
                    : "";
                yield return $"{service} sets Kafka SaslMechanism to '{options.SaslMechanism}', which isn't one the Kafka client supports. " +
                             $"Set {whereToSet("SaslMechanism")} to {Mechanisms}.{iam}";
            }

            var credentials = string.Join(" and ", new[] { hasUsername ? "SaslUsername" : null, hasPassword ? "SaslPassword" : null }.Where(name => name != null));
            if (!hasMechanism)
            {
                if (hasUsername || hasPassword || hasProvider)
                {
                    var settings = string.Join(" and ", new[] { hasUsername || hasPassword ? credentials : null, hasProvider ? "OAuthBearerTokenProvider" : null }.Where(name => name != null));
                    var mechanisms = hasUsername || hasPassword ? UserNameMechanisms : "OAUTHBEARER";
                    yield return $"{service} sets Kafka {settings} but no SaslMechanism, so they aren't used. " +
                                 $"Set {whereToSet("SaslMechanism")} to {mechanisms}, and {whereToSet("SecurityProtocol")} to SASL_SSL.";
                }
                else if (protocolKnown && KafkaSecurity.IsSasl(protocol))
                {
                    yield return $"{service} sets Kafka SecurityProtocol to {options.SecurityProtocol} but no SaslMechanism, so the Kafka client would sign in with Kerberos (GSSAPI). " +
                                 $"Set {whereToSet("SaslMechanism")} to {Mechanisms}.";
                }
            }
            else if (mechanismKnown)
            {
                var name = KafkaSecurity.Name(mechanism);
                if (protocolKnown && !KafkaSecurity.IsSasl(protocol))
                {
                    yield return $"{service} sets Kafka SaslMechanism to {name}, but SecurityProtocol is {protocolName}, which doesn't use SASL. " +
                                 $"Set {whereToSet("SecurityProtocol")} to SASL_SSL, or SASL_PLAINTEXT for a broker without TLS.";
                }

                if (mechanism == SaslMechanism.OAuthBearer)
                {
                    if (!hasProvider)
                    {
                        yield return $"{service} signs in to Kafka with OAUTHBEARER but has no OAuthBearerTokenProvider to get tokens from. {SetTokenProvider}, " +
                                     "for example with tokens signed with AWS IAM for Amazon MSK, or from Microsoft Entra ID for Event Hubs.";
                    }
                }
                else if (hasProvider)
                {
                    yield return $"{service} sets a Kafka OAuthBearerTokenProvider, but SaslMechanism is {name}, so it's never called. " +
                                 $"Set {whereToSet("SaslMechanism")} to OAUTHBEARER to sign in with it, or remove it.";
                }

                if (mechanism == SaslMechanism.Plain || mechanism == SaslMechanism.ScramSha256 || mechanism == SaslMechanism.ScramSha512)
                {
                    if (!hasUsername || !hasPassword)
                    {
                        var missing = !hasUsername && !hasPassword
                            ? $"{whereToSet("SaslUsername")} and {whereToSet("SaslPassword")}"
                            : whereToSet(hasUsername ? "SaslPassword" : "SaslUsername");
                        yield return $"{service} signs in to Kafka with {name}, which needs a user name and password, but they aren't both set. Set {missing}.";
                    }
                }
                else if (hasUsername || hasPassword)
                {
                    yield return $"{service} sets a Kafka {credentials}, but {name} sign-in doesn't use them. " +
                                 $"Remove them, or set {whereToSet("SaslMechanism")} to {UserNameMechanisms} to sign in with them.";
                }
            }

            var tlsSettings = new[]
            {
                string.IsNullOrEmpty(options.SslCaLocation) ? null : "SslCaLocation",
                string.IsNullOrEmpty(options.SslCaPem) ? null : "SslCaPem",
                string.IsNullOrEmpty(options.SslCertificateLocation) ? null : "SslCertificateLocation",
                string.IsNullOrEmpty(options.SslKeyLocation) ? null : "SslKeyLocation",
                string.IsNullOrEmpty(options.SslKeyPassword) ? null : "SslKeyPassword",
                options.EnableSslCertificateVerification.HasValue ? "EnableSslCertificateVerification" : null,
            }.Where(name => name != null).ToList();
            if (tlsSettings.Count > 0 && protocolKnown && !KafkaSecurity.IsTls(protocol))
            {
                yield return $"{service} sets Kafka {string.Join(" and ", tlsSettings)}, but SecurityProtocol is {protocolName}, which doesn't use TLS. " +
                             $"Set {whereToSet("SecurityProtocol")} to SSL or SASL_SSL.";
            }

            foreach (var (setting, path) in new[] { ("SslCaLocation", options.SslCaLocation), ("SslCertificateLocation", options.SslCertificateLocation), ("SslKeyLocation", options.SslKeyLocation) })
            {
                // librdkafka reads a CA location of "probe" as "look in the usual places"
                if (!string.IsNullOrEmpty(path) && !(setting == "SslCaLocation" && path == "probe") && !File.Exists(path) && !Directory.Exists(path))
                {
                    yield return $"{service} sets Kafka {setting} to '{path}', but there's no such file. Set {whereToSet(setting)} to the path of a PEM file" +
                                 (setting == "SslCaLocation" ? ", or remove it to trust the operating system's CAs." : ".");
                }
            }

            foreach (var error in ValidatePlatform(service, options, whereToSet, protocolKnown, protocol, hasMechanism, mechanismKnown, mechanism))
            {
                yield return error;
            }
        }

        private static IEnumerable<string> ValidatePlatform(string service, KafkaOptions options, Func<string, string> whereToSet,
            bool protocolKnown, SecurityProtocol protocol, bool hasMechanism, bool mechanismKnown, SaslMechanism mechanism)
        {
            var servers = Servers(options.BootstrapServers).ToList();

            var eventHubs = servers.Where(server => server.Host.IndexOf(".servicebus.", StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            if (eventHubs.Count > 0)
            {
                if (eventHubs.Any(server => server.Port != 9093))
                {
                    yield return $"{service} connects to Azure Event Hubs, whose Kafka endpoint listens on port 9093, but BootstrapServers is '{options.BootstrapServers}'. " +
                                 $"Set {whereToSet("BootstrapServers")} to {eventHubs[0].Host}:9093.";
                }
                if (protocolKnown && protocol != SecurityProtocol.SaslSsl)
                {
                    yield return $"{service} connects to Azure Event Hubs, which needs SASL over TLS. Set {whereToSet("SecurityProtocol")} to SASL_SSL, and " +
                                 $"{whereToSet("SaslMechanism")} to PLAIN, with {whereToSet("SaslUsername")} $ConnectionString and the namespace's connection string as " +
                                 $"{whereToSet("SaslPassword")}, or to OAUTHBEARER, with an OAuthBearerTokenProvider that gets tokens from Microsoft Entra ID.";
                }
                else if (mechanismKnown && mechanism == SaslMechanism.Plain && !string.IsNullOrEmpty(options.SaslUsername) && options.SaslUsername != "$ConnectionString")
                {
                    yield return $"{service} signs in to Azure Event Hubs with PLAIN, which takes the user name $ConnectionString and the namespace's connection string " +
                                 $"as the password, but SaslUsername is '{options.SaslUsername}'. Set {whereToSet("SaslUsername")} to $ConnectionString.";
                }
                else if (mechanismKnown && mechanism != SaslMechanism.Plain && mechanism != SaslMechanism.OAuthBearer)
                {
                    yield return $"{service} signs in to Azure Event Hubs with {KafkaSecurity.Name(mechanism)}, but Event Hubs supports PLAIN or OAUTHBEARER. " +
                                 $"Set {whereToSet("SaslMechanism")} to PLAIN, with the user name $ConnectionString and the namespace's connection string as the password, " +
                                 "or to OAUTHBEARER, with an OAuthBearerTokenProvider that gets tokens from Microsoft Entra ID.";
                }
            }

            if (servers.Any(server => server.Host.EndsWith(".confluent.cloud", StringComparison.OrdinalIgnoreCase)) && protocolKnown && protocol != SecurityProtocol.SaslSsl)
            {
                yield return $"{service} connects to Confluent Cloud, which needs SASL over TLS. Set {whereToSet("SecurityProtocol")} to SASL_SSL and " +
                             $"{whereToSet("SaslMechanism")} to PLAIN, with an API key and secret as {whereToSet("SaslUsername")} and {whereToSet("SaslPassword")}, " +
                             "or to OAUTHBEARER, with an OAuthBearerTokenProvider.";
            }

            if (protocolKnown && (!hasMechanism || mechanismKnown))
            {
                var mskPorts = servers.Where(IsMsk).Select(server => server.Port).Distinct();
                foreach (var port in mskPorts)
                {
                    if (port is int value && MskPorts.TryGetValue(value > 9100 ? value - 100 : value, out var expected)
                        && (protocol != expected.Protocol || (hasMechanism ? mechanism : (SaslMechanism?)null) != expected.Mechanism))
                    {
                        var fix = expected.Fix
                            .Replace("{SecurityProtocol}", whereToSet("SecurityProtocol"))
                            .Replace("{SaslMechanism}", whereToSet("SaslMechanism"));
                        yield return $"{service} connects to Amazon MSK on port {value}, which is for {expected.AccessControl} access control. {fix}.";
                    }
                }
            }
        }

        private static bool IsMsk((string Host, int? Port) server)
        {
            return (server.Host.IndexOf(".kafka.", StringComparison.OrdinalIgnoreCase) >= 0 || server.Host.IndexOf(".kafka-serverless.", StringComparison.OrdinalIgnoreCase) >= 0)
                   && server.Host.IndexOf(".amazonaws.com", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // host:port pairs, from a list such as "SASL_SSL://b-1.example:9096,b-2.example:9096"
        private static IEnumerable<(string Host, int? Port)> Servers(string bootstrapServers)
        {
            foreach (var entry in (bootstrapServers ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(entry => entry.Trim()))
            {
                var server = entry.Contains("://") ? entry.Substring(entry.IndexOf("://", StringComparison.Ordinal) + 3) : entry;
                var colon = server.LastIndexOf(':');
                if (colon > 0 && int.TryParse(server.Substring(colon + 1), out var port))
                {
                    yield return (server.Substring(0, colon), port);
                }
                else
                {
                    yield return (server, null);
                }
            }
        }
    }
}
