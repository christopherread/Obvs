using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Obvs.Hosting;
using Obvs.NATS.Tests.Hosting;
using Obvs.Serialization.Json;
using Xunit;

namespace Obvs.NATS.Tests.JetStream
{
    // Configuration and validation; none of these connect to a server
    public class TestUseNatsJetStream
    {
        [Fact]
        public void ShouldBindOptionsFromConfiguration()
        {
            var configuration = Configuration(new Dictionary<string, string>
            {
                ["Obvs:Orders:NatsJetStream:Url"] = "nats://nats.example:4222",
                ["Obvs:Orders:NatsJetStream:MaxDeliveryAttempts"] = "3",
                ["Obvs:Orders:NatsJetStream:MaxConcurrentMessages"] = "4",
                ["Obvs:Orders:NatsJetStream:AckWait"] = "00:00:45",
                ["Obvs:Orders:NatsJetStream:MaxInProgressDuration"] = "00:10:00",
            });
            using var host = BuildHost(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service
                    .UseNatsJetStream(configuration.GetSection("Obvs:Orders:NatsJetStream"))
                    .SerializedWith(new JsonMessageSerializer(), new JsonMessageDeserializerFactory(typeof(JsonMessageDeserializer<>))))));

            var options = host.Services.GetRequiredService<IOptionsMonitor<NatsJetStreamOptions>>().Get("orders");
            Assert.Equal("nats://nats.example:4222", options.Url);
            Assert.Equal(3, options.MaxDeliveryAttempts);
            Assert.Equal(4, options.MaxConcurrentMessages);
            Assert.Equal(TimeSpan.FromSeconds(45), options.AckWait);
            Assert.Equal(TimeSpan.FromMinutes(10), options.MaxInProgressDuration);
        }

        [Fact]
        public void ShouldConfigureOptionsInCode()
        {
            using var host = BuildHost(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service
                    .UseNatsJetStream(options => options.Url = "nats://nats.example:4222")
                    .SerializedWith(new JsonMessageSerializer(), new JsonMessageDeserializerFactory(typeof(JsonMessageDeserializer<>))))));

            Assert.Equal("nats://nats.example:4222", host.Services.GetRequiredService<IOptionsMonitor<NatsJetStreamOptions>>().Get("orders").Url);
        }

        [Fact]
        public void ShouldDeliverOneMessageAtATimeTenTimesAndKeepItInProgressForFiveMinutesByDefault()
        {
            var options = new NatsJetStreamOptions();

            Assert.Equal(10, options.MaxDeliveryAttempts);
            Assert.Equal(1, options.MaxConcurrentMessages);
            Assert.Null(options.AckWait);
            Assert.Equal(TimeSpan.FromMinutes(5), options.MaxInProgressDuration);
        }

        [Fact]
        public async Task ShouldFailToStartWhenTheUrlIsMissingFromConfiguration()
        {
            var configuration = Configuration(new Dictionary<string, string>());
            using var host = BuildHost(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service
                    .UseNatsJetStream(configuration.GetSection("Obvs:Orders:NatsJetStream"))
                    .SerializedWith(new JsonMessageSerializer(), new JsonMessageDeserializerFactory(typeof(JsonMessageDeserializer<>))))));

            var exception = await Assert.ThrowsAsync<ObvsConfigurationException>(() => host.StartAsync(TestContext.Current.CancellationToken));

            var error = Assert.Single(exception.Errors);
            Assert.Contains("'orders'", error);
            Assert.Contains("Obvs:Orders:NatsJetStream:Url", error);
        }

        [Fact]
        public async Task ShouldFailToStartWhenTheUrlIsMissingFromCode()
        {
            var error = Assert.Single(await StartupErrorsAsync(options => options.Url = null));

            Assert.Contains("'orders'", error);
            Assert.Contains("options.Url", error);
        }

        [Fact]
        public async Task ShouldFailToStartWithoutASerializer()
        {
            using var host = BuildHost(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service.UseNatsJetStream(options => options.Url = "nats://nats.example:4222"))));

            var exception = await Assert.ThrowsAsync<ObvsConfigurationException>(() => host.StartAsync(TestContext.Current.CancellationToken));

            var error = Assert.Single(exception.Errors);
            Assert.Contains("'orders'", error);
            Assert.Contains("SerializedWith", error);
        }

        [Fact]
        public async Task ShouldFailToStartWhenMaxDeliveryAttemptsIsLessThanOne()
        {
            var error = Assert.Single(await StartupErrorsAsync(options => options.MaxDeliveryAttempts = 0));

            Assert.Contains("'orders'", error);
            Assert.Contains("MaxDeliveryAttempts", error);
            Assert.Contains("at least 1", error);
        }

        [Fact]
        public async Task ShouldFailToStartWhenMaxConcurrentMessagesIsLessThanOne()
        {
            var error = Assert.Single(await StartupErrorsAsync(options => options.MaxConcurrentMessages = 0));

            Assert.Contains("'orders'", error);
            Assert.Contains("MaxConcurrentMessages", error);
            Assert.Contains("at least 1", error);
        }

        [Theory]
        [InlineData("00:00:00")]
        [InlineData("-00:00:01")]
        public async Task ShouldFailToStartWhenAckWaitIsNotPositive(string ackWait)
        {
            var error = Assert.Single(await StartupErrorsAsync(options => options.AckWait = TimeSpan.Parse(ackWait)));

            Assert.Contains("'orders'", error);
            Assert.Contains("AckWait", error);
        }

        [Fact]
        public async Task ShouldFailToStartWhenMaxInProgressDurationIsNegative()
        {
            var error = Assert.Single(await StartupErrorsAsync(options => options.MaxInProgressDuration = TimeSpan.FromSeconds(-1)));

            Assert.Contains("'orders'", error);
            Assert.Contains("MaxInProgressDuration", error);
        }

        [Theory]
        [InlineData("acme.orders")]
        [InlineData("acme orders")]
        [InlineData("orders>")]
        public async Task ShouldFailToStartWhenTheServiceNameCantNameAStream(string serviceName)
        {
            using var host = BuildHost(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>(serviceName, service => service
                    .UseNatsJetStream(options => options.Url = "nats://nats.example:4222")
                    .SerializedWith(new JsonMessageSerializer(), new JsonMessageDeserializerFactory(typeof(JsonMessageDeserializer<>))))));

            var exception = await Assert.ThrowsAsync<ObvsConfigurationException>(() => host.StartAsync(TestContext.Current.CancellationToken));

            var error = Assert.Single(exception.Errors);
            Assert.Contains($"'{serviceName}'", error);
            Assert.Contains("stream", error);
        }

        [Theory]
        [InlineData("worker.1")]
        [InlineData("worker 1")]
        [InlineData("worker*")]
        public async Task ShouldFailToStartWhenTheInstanceIdCantNameAConsumer(string instanceId)
        {
            // This bus's own consumers are named after its instance ID
            using var host = BuildHost(services => services.AddObvs(obvs => obvs
                .UseInstanceId(instanceId)
                .AddService<IOrderMessage>("orders", service => service
                    .UseNatsJetStream(options => options.Url = "nats://nats.example:4222")
                    .SerializedWith(new JsonMessageSerializer(), new JsonMessageDeserializerFactory(typeof(JsonMessageDeserializer<>))))));

            var exception = await Assert.ThrowsAsync<ObvsConfigurationException>(() => host.StartAsync(TestContext.Current.CancellationToken));

            var error = Assert.Single(exception.Errors);
            Assert.Contains("'orders'", error);
            Assert.Contains($"'{instanceId}'", error);
            Assert.Contains("UseInstanceId", error);
        }

        // Starts a host whose options are valid apart from what configure sets, and returns the startup errors
        private static async Task<IReadOnlyList<string>> StartupErrorsAsync(Action<NatsJetStreamOptions> configure)
        {
            using var host = BuildHost(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service
                    .UseNatsJetStream(options =>
                    {
                        options.Url = "nats://nats.example:4222";
                        configure(options);
                    })
                    .SerializedWith(new JsonMessageSerializer(), new JsonMessageDeserializerFactory(typeof(JsonMessageDeserializer<>))))));
            var exception = await Assert.ThrowsAsync<ObvsConfigurationException>(() => host.StartAsync(TestContext.Current.CancellationToken));
            return exception.Errors;
        }

        private static IConfiguration Configuration(IDictionary<string, string> values)
        {
            return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        }

        private static IHost BuildHost(Action<IServiceCollection> configure)
        {
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
            configure(builder.Services);
            return builder.Build();
        }
    }
}
