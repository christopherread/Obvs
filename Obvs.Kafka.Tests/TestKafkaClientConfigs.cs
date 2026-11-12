using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using Obvs.Kafka.Configuration;
using Obvs.Serialization;
using Obvs.Serialization.Json;
using Xunit;
using IMessage = Obvs.Types.IMessage;

namespace Obvs.Kafka.Tests
{
    // The security settings reach the config of every client Obvs creates; none of these connect to a broker
    public class TestKafkaClientConfigs
    {
        public interface ITestMessage : IMessage
        {
        }

        private static KafkaOptions SecureOptions() => new KafkaOptions
        {
            BootstrapServers = "kafka.example:9093",
            SecurityProtocol = "SASL_SSL",
            SaslMechanism = "SCRAM-SHA-512",
            SaslUsername = "orders",
            SaslPassword = "secret",
            SslCaLocation = "/etc/ssl/ca.pem",
            SslCaPem = "-----BEGIN CERTIFICATE-----",
            SslCertificateLocation = "/etc/ssl/client.pem",
            SslKeyLocation = "/etc/ssl/client.key",
            SslKeyPassword = "key-secret",
            EnableSslCertificateVerification = false,
        };

        [Fact]
        public void ShouldApplySecuritySettingsToTheProducerConfig()
        {
            var publisher = new MessagePublisher<ITestMessage>(new KafkaConfiguration(SecureOptions()), new KafkaProducerConfiguration(), "orders.Commands", new JsonMessageSerializer(), null);

            AssertSecure(publisher.ProducerConfig);
            Assert.Equal(1000, publisher.ProducerConfig.BatchNumMessages);
        }

        [Fact]
        public void ShouldApplySecuritySettingsToTheConsumerConfig()
        {
            var source = new MessageSource<ITestMessage>(new KafkaConfiguration(SecureOptions()), new KafkaSourceConfiguration(), "orders.Commands", new IMessageDeserializer<ITestMessage>[0], null);

            AssertSecure(source.ConsumerConfig);
            Assert.Equal(AutoOffsetReset.Latest, source.ConsumerConfig.AutoOffsetReset);
        }

        [Fact]
        public void ShouldApplySecuritySettingsToTheAdminClientConfig()
        {
            AssertSecure(KafkaTopics.AdminClientConfig(new KafkaConfiguration(SecureOptions())));
        }

        [Fact]
        public void ShouldLeaveSecurityToTheDefaultsWithoutSecuritySettings()
        {
            // As v6's ConnectToKafka(...) does, and UseKafka with only BootstrapServers
            foreach (var configuration in new[] { new KafkaConfiguration("localhost:9092"), new KafkaConfiguration(new KafkaOptions { BootstrapServers = "localhost:9092" }) })
            {
                var config = KafkaTopics.AdminClientConfig(configuration);

                Assert.Equal("localhost:9092", config.BootstrapServers);
                Assert.Null(config.SecurityProtocol);
                Assert.Null(config.SaslMechanism);
                Assert.Null(config.SaslUsername);
                Assert.Null(config.SslCaLocation);
                Assert.Null(config.EnableSslCertificateVerification);
                Assert.Null(configuration.OAuthBearerTokenRefreshHandler);
            }
        }

        [Theory]
        [InlineData("PLAINTEXT", SecurityProtocol.Plaintext)]
        [InlineData("ssl", SecurityProtocol.Ssl)]
        [InlineData("SASL_PLAINTEXT", SecurityProtocol.SaslPlaintext)]
        [InlineData("SaslSsl", SecurityProtocol.SaslSsl)]
        public void ShouldReadSecurityProtocolsInKafkasOrConfluentsSpelling(string value, SecurityProtocol expected)
        {
            var config = KafkaTopics.AdminClientConfig(new KafkaConfiguration(new KafkaOptions { BootstrapServers = "localhost:9092", SecurityProtocol = value }));

            Assert.Equal(expected, config.SecurityProtocol);
        }

        [Theory]
        [InlineData("GSSAPI", SaslMechanism.Gssapi)]
        [InlineData("plain", SaslMechanism.Plain)]
        [InlineData("SCRAM-SHA-256", SaslMechanism.ScramSha256)]
        [InlineData("ScramSha512", SaslMechanism.ScramSha512)]
        [InlineData("OAUTHBEARER", SaslMechanism.OAuthBearer)]
        public void ShouldReadSaslMechanismsInKafkasOrConfluentsSpelling(string value, SaslMechanism expected)
        {
            var config = KafkaTopics.AdminClientConfig(new KafkaConfiguration(new KafkaOptions { BootstrapServers = "localhost:9092", SecurityProtocol = "SASL_SSL", SaslMechanism = value }));

            Assert.Equal(expected, config.SaslMechanism);
        }

        [Fact]
        public void ShouldHaveATokenRefreshHandlerWithATokenProvider()
        {
            var options = new KafkaOptions
            {
                BootstrapServers = "localhost:9092",
                SecurityProtocol = "SASL_SSL",
                SaslMechanism = "OAUTHBEARER",
                OAuthBearerTokenProvider = _ => Task.FromResult(new KafkaOAuthBearerToken("token", DateTimeOffset.UtcNow.AddMinutes(15))),
            };

            Assert.NotNull(new KafkaConfiguration(options).OAuthBearerTokenRefreshHandler);
        }

        [Fact]
        public void ShouldSetTheProvidersToken()
        {
            var expires = new DateTimeOffset(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);
            var extensions = new Dictionary<string, string> { ["logicalCluster"] = "lkc-abc12" };
            (string Value, long LifetimeMs, string Principal, IDictionary<string, string> Extensions) set = default;
            string failure = null;

            KafkaOAuthBearer.Refresh(
                _ => Task.FromResult(new KafkaOAuthBearerToken("token-1", expires) { PrincipalName = "orders-app", Extensions = extensions }),
                (value, lifetimeMs, principal, ext) => set = (value, lifetimeMs, principal, ext),
                error => failure = error);

            Assert.Null(failure);
            Assert.Equal("token-1", set.Value);
            Assert.Equal(expires.ToUnixTimeMilliseconds(), set.LifetimeMs);
            Assert.Equal("orders-app", set.Principal);
            Assert.Same(extensions, set.Extensions);
        }

        [Fact]
        public void ShouldNameAPrincipalWhenTheTokenDoesnt()
        {
            string principal = null;

            KafkaOAuthBearer.Refresh(
                _ => Task.FromResult(new KafkaOAuthBearerToken("token-1", DateTimeOffset.UtcNow.AddMinutes(15))),
                (value, lifetimeMs, p, ext) => principal = p,
                error => { });

            Assert.False(string.IsNullOrEmpty(principal));
        }

        [Fact]
        public void ShouldReportTheProvidersFailure()
        {
            string failure = null;
            var setToken = false;

            KafkaOAuthBearer.Refresh(
                _ => throw new InvalidOperationException("No AWS credentials found"),
                (value, lifetimeMs, principal, ext) => setToken = true,
                error => failure = error);

            Assert.False(setToken);
            Assert.Contains("No AWS credentials found", failure);
        }

        [Fact]
        public void ShouldReportAFaultedProviderTask()
        {
            string failure = null;

            KafkaOAuthBearer.Refresh(
                async _ =>
                {
                    await Task.Yield();
                    throw new InvalidOperationException("Token endpoint returned 401");
                },
                (value, lifetimeMs, principal, ext) => { },
                error => failure = error);

            Assert.Contains("Token endpoint returned 401", failure);
        }

        [Fact]
        public void ShouldReportAMissingToken()
        {
            string failure = null;

            KafkaOAuthBearer.Refresh(_ => Task.FromResult<KafkaOAuthBearerToken>(null), (value, lifetimeMs, principal, ext) => { }, error => failure = error);

            Assert.Contains("OAuthBearerTokenProvider", failure);
        }

        [Fact]
        public void ShouldGiveTheProviderACancellationTokenThatTimesOut()
        {
            var token = CancellationToken.None;

            KafkaOAuthBearer.Refresh(
                ct =>
                {
                    token = ct;
                    return Task.FromResult(new KafkaOAuthBearerToken("token-1", DateTimeOffset.UtcNow.AddMinutes(15)));
                },
                (value, lifetimeMs, principal, ext) => { },
                error => { });

            Assert.True(token.CanBeCanceled);
        }

        [Fact]
        public void ShouldPointAtTheCredentialsWhenSignInIsRefused()
        {
            var message = KafkaTopics.SignInFailure("orders", new KafkaConfiguration(SecureOptions()), new Error(ErrorCode.Local_Authentication, "SASL authentication error: Authentication failed"));

            Assert.Contains("'orders'", message);
            Assert.Contains("kafka.example:9093", message);
            Assert.Contains("Authentication failed", message);
            Assert.Contains("SaslPassword", message);
        }

        [Fact]
        public void ShouldPointAtTheTlsSettingsWhenTheTlsHandshakeFails()
        {
            var message = KafkaTopics.SignInFailure("orders", new KafkaConfiguration(SecureOptions()), new Error(ErrorCode.Local_Ssl, "SSL handshake failed: certificate verify failed"));

            Assert.Contains("'orders'", message);
            Assert.Contains("certificate verify failed", message);
            Assert.Contains("SslCaLocation", message);
        }

        private static void AssertSecure(ClientConfig config)
        {
            Assert.Equal("kafka.example:9093", config.BootstrapServers);
            Assert.Equal(SecurityProtocol.SaslSsl, config.SecurityProtocol);
            Assert.Equal(SaslMechanism.ScramSha512, config.SaslMechanism);
            Assert.Equal("orders", config.SaslUsername);
            Assert.Equal("secret", config.SaslPassword);
            Assert.Equal("/etc/ssl/ca.pem", config.SslCaLocation);
            Assert.Equal("-----BEGIN CERTIFICATE-----", config.SslCaPem);
            Assert.Equal("/etc/ssl/client.pem", config.SslCertificateLocation);
            Assert.Equal("/etc/ssl/client.key", config.SslKeyLocation);
            Assert.Equal("key-secret", config.SslKeyPassword);
            Assert.False(config.EnableSslCertificateVerification);
        }
    }
}
