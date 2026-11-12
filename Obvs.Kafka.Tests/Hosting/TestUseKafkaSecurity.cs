using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Obvs.Hosting;
using Obvs.Kafka.Configuration;
using Obvs.Serialization.Json;
using Xunit;

namespace Obvs.Kafka.Tests.Hosting
{
    // Security settings and their startup checks; none of these connect to a broker
    public class TestUseKafkaSecurity
    {
        private const string Section = "Obvs:Orders:Kafka";

        [Fact]
        public void ShouldBindSecuritySettingsFromConfiguration()
        {
            var configuration = Configuration(new Dictionary<string, string>
            {
                [$"{Section}:BootstrapServers"] = "kafka.example:9093",
                [$"{Section}:SecurityProtocol"] = "SASL_SSL",
                [$"{Section}:SaslMechanism"] = "SCRAM-SHA-512",
                [$"{Section}:SaslUsername"] = "orders",
                [$"{Section}:SaslPassword"] = "secret",
                [$"{Section}:SslCaLocation"] = "/etc/ssl/ca.pem",
                [$"{Section}:SslCaPem"] = "-----BEGIN CERTIFICATE-----",
                [$"{Section}:SslCertificateLocation"] = "/etc/ssl/client.pem",
                [$"{Section}:SslKeyLocation"] = "/etc/ssl/client.key",
                [$"{Section}:SslKeyPassword"] = "key-secret",
                [$"{Section}:EnableSslCertificateVerification"] = "false",
            });
            using var host = BuildHost(service => service.UseKafka(configuration.GetSection(Section)));

            var options = host.Services.GetRequiredService<IOptionsMonitor<KafkaOptions>>().Get("orders");

            Assert.Equal("SASL_SSL", options.SecurityProtocol);
            Assert.Equal("SCRAM-SHA-512", options.SaslMechanism);
            Assert.Equal("orders", options.SaslUsername);
            Assert.Equal("secret", options.SaslPassword);
            Assert.Equal("/etc/ssl/ca.pem", options.SslCaLocation);
            Assert.Equal("-----BEGIN CERTIFICATE-----", options.SslCaPem);
            Assert.Equal("/etc/ssl/client.pem", options.SslCertificateLocation);
            Assert.Equal("/etc/ssl/client.key", options.SslKeyLocation);
            Assert.Equal("key-secret", options.SslKeyPassword);
            Assert.False(options.EnableSslCertificateVerification);
        }

        [Fact]
        public void ShouldBindFromConfigurationAndSetATokenProviderInCode()
        {
            var configuration = Configuration(new Dictionary<string, string>
            {
                [$"{Section}:BootstrapServers"] = "b-1.orders.abc123.c2.kafka.us-east-1.amazonaws.com:9098",
                [$"{Section}:SecurityProtocol"] = "SASL_SSL",
                [$"{Section}:SaslMechanism"] = "OAUTHBEARER",
            });
            Func<System.Threading.CancellationToken, Task<KafkaOAuthBearerToken>> provider = _ => Task.FromResult(new KafkaOAuthBearerToken("token", DateTimeOffset.UtcNow.AddMinutes(15)));
            using var host = BuildHost(service => service.UseKafka(configuration.GetSection(Section), options => options.OAuthBearerTokenProvider = provider));

            var options = host.Services.GetRequiredService<IOptionsMonitor<KafkaOptions>>().Get("orders");

            Assert.Equal("b-1.orders.abc123.c2.kafka.us-east-1.amazonaws.com:9098", options.BootstrapServers);
            Assert.Equal("OAUTHBEARER", options.SaslMechanism);
            Assert.Same(provider, options.OAuthBearerTokenProvider);
        }

        [Fact]
        public async Task ShouldFailToStartWithAnUnknownSecurityProtocol()
        {
            var error = await StartupErrorAsync(new Dictionary<string, string> { ["SecurityProtocol"] = "TLS" });

            Assert.Contains("'orders'", error);
            Assert.Contains("'TLS'", error);
            Assert.Contains($"{Section}:SecurityProtocol", error);
            Assert.Contains("PLAINTEXT, SSL, SASL_PLAINTEXT or SASL_SSL", error);
        }

        [Fact]
        public async Task ShouldFailToStartWithAnUnknownSaslMechanism()
        {
            var error = await StartupErrorAsync(new Dictionary<string, string>
            {
                ["SecurityProtocol"] = "SASL_SSL",
                ["SaslMechanism"] = "AWS_MSK_IAM",
            });

            Assert.Contains("'orders'", error);
            Assert.Contains("'AWS_MSK_IAM'", error);
            Assert.Contains($"{Section}:SaslMechanism", error);
            Assert.Contains("PLAIN, SCRAM-SHA-256, SCRAM-SHA-512 or OAUTHBEARER", error);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("PLAINTEXT")]
        [InlineData("SSL")]
        public async Task ShouldFailToStartWithASaslMechanismButNoSaslProtocol(string protocol)
        {
            var error = await StartupErrorAsync(new Dictionary<string, string>
            {
                ["SecurityProtocol"] = protocol,
                ["SaslMechanism"] = "PLAIN",
                ["SaslUsername"] = "orders",
                ["SaslPassword"] = "secret",
            });

            Assert.Contains("'orders'", error);
            Assert.Contains("PLAIN", error);
            Assert.Contains($"{Section}:SecurityProtocol", error);
            Assert.Contains("SASL_SSL", error);
        }

        [Theory]
        [InlineData("SASL_SSL")]
        [InlineData("SASL_PLAINTEXT")]
        public async Task ShouldFailToStartWithASaslProtocolButNoMechanism(string protocol)
        {
            var error = await StartupErrorAsync(new Dictionary<string, string> { ["SecurityProtocol"] = protocol });

            Assert.Contains("'orders'", error);
            Assert.Contains($"{Section}:SaslMechanism", error);
            Assert.Contains("PLAIN, SCRAM-SHA-256, SCRAM-SHA-512 or OAUTHBEARER", error);
        }

        [Theory]
        [InlineData("PLAIN", null, "secret", "SaslUsername")]
        [InlineData("SCRAM-SHA-256", "orders", null, "SaslPassword")]
        [InlineData("SCRAM-SHA-512", null, null, "SaslUsername")]
        public async Task ShouldFailToStartWithAUserNameAndPasswordMechanismButNoCredentials(string mechanism, string username, string password, string missing)
        {
            var error = await StartupErrorAsync(new Dictionary<string, string>
            {
                ["SecurityProtocol"] = "SASL_SSL",
                ["SaslMechanism"] = mechanism,
                ["SaslUsername"] = username,
                ["SaslPassword"] = password,
            });

            Assert.Contains("'orders'", error);
            Assert.Contains(mechanism, error);
            Assert.Contains($"{Section}:{missing}", error);
        }

        [Fact]
        public async Task ShouldFailToStartWithOAuthBearerButNoTokenProvider()
        {
            var error = await StartupErrorAsync(new Dictionary<string, string>
            {
                ["SecurityProtocol"] = "SASL_SSL",
                ["SaslMechanism"] = "OAUTHBEARER",
            });

            Assert.Contains("'orders'", error);
            Assert.Contains("OAUTHBEARER", error);
            Assert.Contains("OAuthBearerTokenProvider", error);
            Assert.Contains("UseKafka(", error);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("PLAIN")]
        public async Task ShouldFailToStartWithATokenProviderButNoOAuthBearer(string mechanism)
        {
            var error = await StartupErrorAsync(new Dictionary<string, string>
            {
                ["SecurityProtocol"] = mechanism == null ? null : "SASL_SSL",
                ["SaslMechanism"] = mechanism,
                ["SaslUsername"] = mechanism == null ? null : "orders",
                ["SaslPassword"] = mechanism == null ? null : "secret",
            }, options => options.OAuthBearerTokenProvider = _ => Task.FromResult<KafkaOAuthBearerToken>(null));

            Assert.Contains("'orders'", error);
            Assert.Contains("OAuthBearerTokenProvider", error);
            Assert.Contains($"{Section}:SaslMechanism", error);
            Assert.Contains("OAUTHBEARER", error);
        }

        [Fact]
        public async Task ShouldFailToStartWithCredentialsTheMechanismDoesntUse()
        {
            var error = await StartupErrorAsync(new Dictionary<string, string>
            {
                ["SecurityProtocol"] = "SASL_SSL",
                ["SaslMechanism"] = "OAUTHBEARER",
                ["SaslPassword"] = "secret",
            }, options => options.OAuthBearerTokenProvider = _ => Task.FromResult<KafkaOAuthBearerToken>(null));

            Assert.Contains("'orders'", error);
            Assert.Contains("SaslPassword", error);
            Assert.Contains("OAUTHBEARER", error);
        }

        [Fact]
        public async Task ShouldFailToStartWithCredentialsButNoMechanism()
        {
            var error = await StartupErrorAsync(new Dictionary<string, string>
            {
                ["SaslUsername"] = "orders",
                ["SaslPassword"] = "secret",
            });

            Assert.Contains("'orders'", error);
            Assert.Contains("SaslUsername", error);
            Assert.Contains($"{Section}:SaslMechanism", error);
            Assert.Contains($"{Section}:SecurityProtocol", error);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("PLAINTEXT")]
        [InlineData("SASL_PLAINTEXT")]
        public async Task ShouldFailToStartWithTlsSettingsButNoTls(string protocol)
        {
            var error = await StartupErrorAsync(new Dictionary<string, string>
            {
                ["SecurityProtocol"] = protocol,
                ["SaslMechanism"] = protocol == "SASL_PLAINTEXT" ? "PLAIN" : null,
                ["SaslUsername"] = protocol == "SASL_PLAINTEXT" ? "orders" : null,
                ["SaslPassword"] = protocol == "SASL_PLAINTEXT" ? "secret" : null,
                ["SslCaPem"] = "-----BEGIN CERTIFICATE-----",
            });

            Assert.Contains("'orders'", error);
            Assert.Contains("SslCaPem", error);
            Assert.Contains($"{Section}:SecurityProtocol", error);
            Assert.Contains("SSL or SASL_SSL", error);
        }

        [Theory]
        [InlineData("SslCaLocation")]
        [InlineData("SslCertificateLocation")]
        [InlineData("SslKeyLocation")]
        public async Task ShouldFailToStartWhenATlsFileDoesntExist(string setting)
        {
            var path = Path.Combine(Path.GetTempPath(), $"obvs-missing-{Guid.NewGuid():N}.pem");

            var error = await StartupErrorAsync(new Dictionary<string, string>
            {
                ["SecurityProtocol"] = "SSL",
                [setting] = path,
            });

            Assert.Contains("'orders'", error);
            Assert.Contains($"{Section}:{setting}", error);
            Assert.Contains(path, error);
        }

        [Fact]
        public void ShouldAcceptAnExistingCaFileAndTheProbeSetting()
        {
            var path = Path.GetTempFileName();
            try
            {
                Assert.Empty(Validate(new KafkaOptions { BootstrapServers = "kafka.example:9093", SecurityProtocol = "SSL", SslCaLocation = path }));
                Assert.Empty(Validate(new KafkaOptions { BootstrapServers = "kafka.example:9093", SecurityProtocol = "SSL", SslCaLocation = "probe" }));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public async Task ShouldFailToStartWhenEventHubsIsntOnPort9093()
        {
            var error = await StartupErrorAsync(new Dictionary<string, string>
            {
                ["BootstrapServers"] = "contoso.servicebus.windows.net:9092",
                ["SecurityProtocol"] = "SASL_SSL",
                ["SaslMechanism"] = "PLAIN",
                ["SaslUsername"] = "$ConnectionString",
                ["SaslPassword"] = "Endpoint=sb://contoso.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abc",
            });

            Assert.Contains("'orders'", error);
            Assert.Contains("Event Hubs", error);
            Assert.Contains("contoso.servicebus.windows.net:9093", error);
            Assert.Contains($"{Section}:BootstrapServers", error);
        }

        [Fact]
        public async Task ShouldFailToStartWhenEventHubsIsntSaslSsl()
        {
            var error = await StartupErrorAsync(new Dictionary<string, string> { ["BootstrapServers"] = "contoso.servicebus.windows.net:9093" });

            Assert.Contains("'orders'", error);
            Assert.Contains("Event Hubs", error);
            Assert.Contains($"{Section}:SecurityProtocol", error);
            Assert.Contains("SASL_SSL", error);
            Assert.Contains("$ConnectionString", error);
            Assert.Contains("OAUTHBEARER", error);
        }

        [Fact]
        public async Task ShouldFailToStartWhenEventHubsPlainIsntSignedInWithTheConnectionString()
        {
            var error = await StartupErrorAsync(new Dictionary<string, string>
            {
                ["BootstrapServers"] = "contoso.servicebus.windows.net:9093",
                ["SecurityProtocol"] = "SASL_SSL",
                ["SaslMechanism"] = "PLAIN",
                ["SaslUsername"] = "RootManageSharedAccessKey",
                ["SaslPassword"] = "abc",
            });

            Assert.Contains("'orders'", error);
            Assert.Contains($"{Section}:SaslUsername", error);
            Assert.Contains("$ConnectionString", error);
        }

        [Fact]
        public async Task ShouldFailToStartWhenEventHubsUsesScram()
        {
            var error = await StartupErrorAsync(new Dictionary<string, string>
            {
                ["BootstrapServers"] = "contoso.servicebus.windows.net:9093",
                ["SecurityProtocol"] = "SASL_SSL",
                ["SaslMechanism"] = "SCRAM-SHA-512",
                ["SaslUsername"] = "orders",
                ["SaslPassword"] = "abc",
            });

            Assert.Contains("'orders'", error);
            Assert.Contains("Event Hubs", error);
            Assert.Contains($"{Section}:SaslMechanism", error);
            Assert.Contains("PLAIN or OAUTHBEARER", error);
        }

        [Fact]
        public async Task ShouldFailToStartWhenConfluentCloudIsntSaslSsl()
        {
            var error = await StartupErrorAsync(new Dictionary<string, string> { ["BootstrapServers"] = "pkc-abc12.us-east-1.aws.confluent.cloud:9092" });

            Assert.Contains("'orders'", error);
            Assert.Contains("Confluent Cloud", error);
            Assert.Contains($"{Section}:SecurityProtocol", error);
            Assert.Contains("SASL_SSL", error);
            Assert.Contains("API key", error);
        }

        [Theory]
        [InlineData("b-1.orders.abc123.c2.kafka.us-east-1.amazonaws.com:9098", "SASL_SSL", "PLAIN", "IAM", "OAUTHBEARER")]
        [InlineData("b-1.orders.abc123.c2.kafka.us-east-1.amazonaws.com:9198", "SASL_SSL", "SCRAM-SHA-512", "IAM", "OAUTHBEARER")]
        [InlineData("boot-abc123.c2.kafka-serverless.us-east-1.amazonaws.com:9098", "SASL_SSL", "PLAIN", "IAM", "OAUTHBEARER")]
        [InlineData("b-1.orders.abc123.c2.kafka.us-east-1.amazonaws.com:9096", "SASL_SSL", "SCRAM-SHA-256", "SCRAM", "SCRAM-SHA-512")]
        [InlineData("b-1.orders.abc123.c2.kafka.us-east-1.amazonaws.com:9094", "SASL_SSL", "PLAIN", "TLS", "SSL")]
        public async Task ShouldFailToStartWhenMskSettingsDontMatchThePort(string bootstrapServers, string protocol, string mechanism, string accessControl, string expected)
        {
            var error = await StartupErrorAsync(new Dictionary<string, string>
            {
                ["BootstrapServers"] = bootstrapServers,
                ["SecurityProtocol"] = protocol,
                ["SaslMechanism"] = mechanism,
                ["SaslUsername"] = "orders",
                ["SaslPassword"] = "secret",
            });

            Assert.Contains("'orders'", error);
            Assert.Contains("Amazon MSK", error);
            Assert.Contains(accessControl, error);
            Assert.Contains(expected, error);
        }

        [Fact]
        public async Task ShouldReportEveryProblemTogether()
        {
            var exception = await StartupExceptionAsync(new Dictionary<string, string>
            {
                ["SecurityProtocol"] = "SASL_SSL",
                ["SaslMechanism"] = "PLAIN",
                ["SslCaLocation"] = Path.Combine(Path.GetTempPath(), $"obvs-missing-{Guid.NewGuid():N}.pem"),
            });

            Assert.Equal(2, exception.Errors.Count);
        }

        // The settings each platform's docs page shows pass the startup checks
        public static TheoryData<string, KafkaOptions> DocumentedSettings => new TheoryData<string, KafkaOptions>
        {
            { "local broker", new KafkaOptions { BootstrapServers = "localhost:9092" } },
            { "local SASL broker", new KafkaOptions { BootstrapServers = "localhost:9094", SecurityProtocol = "SASL_PLAINTEXT", SaslMechanism = "PLAIN", SaslUsername = "obvs", SaslPassword = "obvs-secret" } },
            { "Confluent Cloud", new KafkaOptions { BootstrapServers = "pkc-abc12.us-east-1.aws.confluent.cloud:9092", SecurityProtocol = "SASL_SSL", SaslMechanism = "PLAIN", SaslUsername = "API_KEY", SaslPassword = "API_SECRET" } },
            { "Event Hubs, connection string", new KafkaOptions { BootstrapServers = "contoso.servicebus.windows.net:9093", SecurityProtocol = "SASL_SSL", SaslMechanism = "PLAIN", SaslUsername = "$ConnectionString", SaslPassword = "Endpoint=sb://contoso.servicebus.windows.net/;SharedAccessKeyName=Send;SharedAccessKey=abc" } },
            { "Event Hubs, Entra ID", new KafkaOptions { BootstrapServers = "contoso.servicebus.windows.net:9093", SecurityProtocol = "SASL_SSL", SaslMechanism = "OAUTHBEARER", OAuthBearerTokenProvider = _ => Task.FromResult<KafkaOAuthBearerToken>(null) } },
            { "MSK, IAM", new KafkaOptions { BootstrapServers = "b-1.orders.abc123.c2.kafka.us-east-1.amazonaws.com:9098,b-2.orders.abc123.c2.kafka.us-east-1.amazonaws.com:9098", SecurityProtocol = "SASL_SSL", SaslMechanism = "OAUTHBEARER", OAuthBearerTokenProvider = _ => Task.FromResult<KafkaOAuthBearerToken>(null) } },
            { "MSK, SCRAM", new KafkaOptions { BootstrapServers = "b-1.orders.abc123.c2.kafka.us-east-1.amazonaws.com:9096", SecurityProtocol = "SASL_SSL", SaslMechanism = "SCRAM-SHA-512", SaslUsername = "orders", SaslPassword = "secret" } },
            { "MSK, TLS", new KafkaOptions { BootstrapServers = "b-1.orders.abc123.c2.kafka.us-east-1.amazonaws.com:9094", SecurityProtocol = "SSL" } },
            { "Kafka's own spelling of names", new KafkaOptions { BootstrapServers = "kafka.example:9093", SecurityProtocol = "sasl_ssl", SaslMechanism = "scram-sha-256", SaslUsername = "orders", SaslPassword = "secret" } },
            { "Confluent.Kafka's enum names", new KafkaOptions { BootstrapServers = "kafka.example:9093", SecurityProtocol = "SaslSsl", SaslMechanism = "ScramSha256", SaslUsername = "orders", SaslPassword = "secret" } },
        };

        [Theory]
        [MemberData(nameof(DocumentedSettings))]
        public void ShouldAcceptTheDocumentedSettings(string platform, KafkaOptions options)
        {
            Assert.True(!Validate(options).Any(), $"{platform}: {string.Join(" ", Validate(options))}");
        }

        private static IEnumerable<string> Validate(KafkaOptions options)
        {
            return KafkaSecurityChecks.Validate("orders", options, setting => $"options.{setting}");
        }

        private static async Task<string> StartupErrorAsync(IDictionary<string, string> settings, Action<KafkaOptions> configure = null)
        {
            var exception = await StartupExceptionAsync(settings, configure);
            return Assert.Single(exception.Errors);
        }

        private static async Task<ObvsConfigurationException> StartupExceptionAsync(IDictionary<string, string> settings, Action<KafkaOptions> configure = null)
        {
            var values = new Dictionary<string, string> { [$"{Section}:BootstrapServers"] = "kafka.example:9092" };
            foreach (var setting in settings.Where(setting => setting.Value != null))
            {
                values[$"{Section}:{setting.Key}"] = setting.Value;
            }
            var configuration = Configuration(values);
            using var host = BuildHost(service => service.UseKafka(configuration.GetSection(Section), configure ?? (_ => { })));

            return await Assert.ThrowsAsync<ObvsConfigurationException>(() => host.StartAsync(TestContext.Current.CancellationToken));
        }

        private static IConfiguration Configuration(IDictionary<string, string> values)
        {
            return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        }

        private static IHost BuildHost(Func<ObvsServiceBuilder, ObvsServiceBuilder> useKafka)
        {
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
            builder.Services.AddObvs(obvs => obvs.AddService<IOrderMessage>("orders", service => useKafka(service)
                .SerializedWith(new JsonMessageSerializer(), new JsonMessageDeserializerFactory(typeof(JsonMessageDeserializer<>)))));
            return builder.Build();
        }
    }
}
