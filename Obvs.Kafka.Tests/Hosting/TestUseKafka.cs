using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Obvs.Hosting;
using Obvs.Serialization.Json;
using Xunit;

namespace Obvs.Kafka.Tests.Hosting
{
    // Configuration and validation; none of these connect to a broker
    public class TestUseKafka
    {
        [Fact]
        public void ShouldBindOptionsFromConfiguration()
        {
            var configuration = Configuration(new Dictionary<string, string> { ["Obvs:Orders:Kafka:BootstrapServers"] = "kafka1.example:9092,kafka2.example:9092" });
            using var host = BuildHost(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service
                    .UseKafka(configuration.GetSection("Obvs:Orders:Kafka"))
                    .SerializedWith(new JsonMessageSerializer(), new JsonMessageDeserializerFactory(typeof(JsonMessageDeserializer<>))))));

            Assert.Equal("kafka1.example:9092,kafka2.example:9092", host.Services.GetRequiredService<IOptionsMonitor<KafkaOptions>>().Get("orders").BootstrapServers);
        }

        [Fact]
        public void ShouldConfigureOptionsInCode()
        {
            using var host = BuildHost(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service
                    .UseKafka(options => options.BootstrapServers = "kafka.example:9092")
                    .SerializedWith(new JsonMessageSerializer(), new JsonMessageDeserializerFactory(typeof(JsonMessageDeserializer<>))))));

            Assert.Equal("kafka.example:9092", host.Services.GetRequiredService<IOptionsMonitor<KafkaOptions>>().Get("orders").BootstrapServers);
        }

        [Fact]
        public async Task ShouldFailToStartWhenTheBootstrapServersAreMissingFromConfiguration()
        {
            var configuration = Configuration(new Dictionary<string, string>());
            using var host = BuildHost(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service
                    .UseKafka(configuration.GetSection("Obvs:Orders:Kafka"))
                    .SerializedWith(new JsonMessageSerializer(), new JsonMessageDeserializerFactory(typeof(JsonMessageDeserializer<>))))));

            var exception = await Assert.ThrowsAsync<ObvsConfigurationException>(() => host.StartAsync(TestContext.Current.CancellationToken));

            var error = Assert.Single(exception.Errors);
            Assert.Contains("'orders'", error);
            Assert.Contains("Obvs:Orders:Kafka:BootstrapServers", error);
        }

        [Fact]
        public async Task ShouldFailToStartWhenTheBootstrapServersAreMissingFromCode()
        {
            using var host = BuildHost(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service
                    .UseKafka(options => { })
                    .SerializedWith(new JsonMessageSerializer(), new JsonMessageDeserializerFactory(typeof(JsonMessageDeserializer<>))))));

            var exception = await Assert.ThrowsAsync<ObvsConfigurationException>(() => host.StartAsync(TestContext.Current.CancellationToken));

            var error = Assert.Single(exception.Errors);
            Assert.Contains("'orders'", error);
            Assert.Contains("BootstrapServers", error);
        }

        [Fact]
        public async Task ShouldFailToStartWithoutASerializer()
        {
            using var host = BuildHost(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service.UseKafka(options => options.BootstrapServers = "kafka.example:9092"))));

            var exception = await Assert.ThrowsAsync<ObvsConfigurationException>(() => host.StartAsync(TestContext.Current.CancellationToken));

            var error = Assert.Single(exception.Errors);
            Assert.Contains("'orders'", error);
            Assert.Contains("SerializedWith", error);
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
