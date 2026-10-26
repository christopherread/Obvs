using System;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Obvs.Hosting;
using Obvs.Kafka.Configuration;
using Obvs.Serialization.Json;
using Xunit;

namespace Obvs.Kafka.Tests.Hosting
{
    // End to end through UseKafka against a broker that requires SASL sign-in (KAFKA_SASL_BOOTSTRAP_SERVERS, default
    // localhost:9094), started with: docker compose -f Obvs.Kafka.Tests/SaslBroker/docker-compose.yml up -d
    public class TestKafkaSasl : IClassFixture<TestKafkaSasl.ServiceNames>
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        // Under KafkaTopics' 30 second wait for topics, so a refused sign-in fails without waiting that out
        private static readonly TimeSpan SignInFailureTimeout = TimeSpan.FromSeconds(20);

        private readonly ServiceNames _serviceNames;

        public TestKafkaSasl(ServiceNames serviceNames)
        {
            _serviceNames = serviceNames;
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldSendCommandsAndPublishEventsSigningInWithSaslPlain()
        {
            using var host = await StartHostAsync(_serviceNames.Create("plain"), PlainSignIn(TestBroker.SaslPassword));
            var bus = host.Services.GetRequiredService<IServiceBus>();

            var command = bus.Commands.OfType<PlaceOrder>().FirstAsync().ToTask();
            await bus.SendAsync(new PlaceOrder { OrderId = "o-1" });
            Assert.Equal("o-1", (await command.WaitAsync(Timeout, TestContext.Current.CancellationToken)).OrderId);

            var placed = bus.Events.OfType<OrderPlaced>().FirstAsync().ToTask();
            await bus.PublishAsync(new OrderPlaced { OrderId = "o-1" });
            Assert.Equal("o-1", (await placed.WaitAsync(Timeout, TestContext.Current.CancellationToken)).OrderId);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldFailToStartWithTheWrongSaslPassword()
        {
            var serviceName = _serviceNames.Create("wrong-password");

            var exception = await Assert.ThrowsAsync<ObvsConfigurationException>(() =>
                StartHostAsync(serviceName, PlainSignIn("not-the-password")).WaitAsync(SignInFailureTimeout, TestContext.Current.CancellationToken));

            var error = Assert.Single(exception.Errors);
            Assert.Contains($"'{serviceName}'", error);
            Assert.Contains(TestBroker.SaslBootstrapServers, error);
            Assert.Contains("Authentication failed", error);
            Assert.Contains("SaslUsername", error);
            Assert.Contains("SaslPassword", error);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldSendCommandsSigningInWithAnOAuthBearerToken()
        {
            var tokens = 0;
            using var host = await StartHostAsync(_serviceNames.Create("oauth"), options =>
            {
                options.BootstrapServers = TestBroker.SaslBootstrapServers;
                options.SecurityProtocol = "SASL_PLAINTEXT";
                options.SaslMechanism = "OAUTHBEARER";
                options.OAuthBearerTokenProvider = async ct =>
                {
                    Interlocked.Increment(ref tokens);
                    await Task.Yield();
                    return UnsignedToken("obvs");
                };
            });
            var bus = host.Services.GetRequiredService<IServiceBus>();

            var command = bus.Commands.OfType<PlaceOrder>().FirstAsync().ToTask();
            await bus.SendAsync(new PlaceOrder { OrderId = "o-1" });

            Assert.Equal("o-1", (await command.WaitAsync(Timeout, TestContext.Current.CancellationToken)).OrderId);
            // At least the admin client, the commands consumer and the commands producer each signed in
            Assert.True(Volatile.Read(ref tokens) >= 3, $"The token provider was called {tokens} times.");
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldFailToStartWhenTheTokenProviderFails()
        {
            var serviceName = _serviceNames.Create("oauth-failure");

            var exception = await Assert.ThrowsAsync<ObvsConfigurationException>(() =>
                StartHostAsync(serviceName, options =>
                {
                    options.BootstrapServers = TestBroker.SaslBootstrapServers;
                    options.SecurityProtocol = "SASL_PLAINTEXT";
                    options.SaslMechanism = "OAUTHBEARER";
                    options.OAuthBearerTokenProvider = ct => throw new InvalidOperationException("No AWS credentials found");
                }).WaitAsync(SignInFailureTimeout, TestContext.Current.CancellationToken));

            var error = Assert.Single(exception.Errors);
            Assert.Contains($"'{serviceName}'", error);
            Assert.Contains("No AWS credentials found", error);
            Assert.Contains("OAuthBearerTokenProvider", error);
        }

        private static Action<KafkaOptions> PlainSignIn(string password)
        {
            return options =>
            {
                options.BootstrapServers = TestBroker.SaslBootstrapServers;
                options.SecurityProtocol = "SASL_PLAINTEXT";
                options.SaslMechanism = "PLAIN";
                options.SaslUsername = TestBroker.SaslUsername;
                options.SaslPassword = password;
            };
        }

        // An unsigned JWT, which the test broker's built-in OAUTHBEARER validator accepts. Real platforms need signed ones.
        private static KafkaOAuthBearerToken UnsignedToken(string subject)
        {
            var now = DateTimeOffset.UtcNow;
            var expires = now.AddMinutes(15);
            var header = Base64Url("{\"alg\":\"none\"}");
            var claims = Base64Url($"{{\"sub\":\"{subject}\",\"iat\":{now.ToUnixTimeSeconds()},\"exp\":{expires.ToUnixTimeSeconds()}}}");
            return new KafkaOAuthBearerToken($"{header}.{claims}.", expires) { PrincipalName = subject };
        }

        private static string Base64Url(string json)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        private static async Task<IHost> StartHostAsync(string serviceName, Action<KafkaOptions> configure)
        {
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
            builder.Services.AddObvs(obvs => obvs.AddService<IOrderMessage>(serviceName, service => service
                .UseKafka(configure)
                .SerializedWith(new JsonMessageSerializer(), new JsonMessageDeserializerFactory(typeof(JsonMessageDeserializer<>)))));
            var host = builder.Build();
            try
            {
                await host.StartAsync(TestContext.Current.CancellationToken);
                return host;
            }
            catch
            {
                host.Dispose();
                throw;
            }
        }

        // Names services uniquely for this run, and deletes their topics afterwards
        public sealed class ServiceNames : IAsyncDisposable
        {
            private readonly string _runId = Guid.NewGuid().ToString("N").Substring(0, 8);

            public string Create(string name) => $"obvs-kafka-sasl-{name}-{_runId}";

            public async ValueTask DisposeAsync()
            {
                var options = new KafkaOptions();
                PlainSignIn(TestBroker.SaslPassword)(options);
                using var admin = new AdminClientBuilder(KafkaTopics.AdminClientConfig(new KafkaConfiguration(options))).Build();
                var topics = admin.GetMetadata(Timeout).Topics.Select(topic => topic.Topic).Where(topic => topic.StartsWith("obvs-kafka-sasl-") && topic.Contains(_runId)).ToList();
                if (topics.Count > 0)
                {
                    await admin.DeleteTopicsAsync(topics);
                }
            }
        }
    }
}
