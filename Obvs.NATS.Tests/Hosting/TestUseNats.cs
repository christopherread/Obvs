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

namespace Obvs.NATS.Tests.Hosting
{
    // Configuration and validation; none of these connect to a server
    public class TestUseNats
    {
        [Fact]
        public void ShouldBindOptionsFromConfiguration()
        {
            var configuration = Configuration(new Dictionary<string, string> { ["Obvs:Orders:Nats:Url"] = "nats://nats.example:4222" });
            using var host = BuildHost(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service
                    .UseNats(configuration.GetSection("Obvs:Orders:Nats"))
                    .SerializedWith(new JsonMessageSerializer(), new JsonMessageDeserializerFactory(typeof(JsonMessageDeserializer<>))))));

            Assert.Equal("nats://nats.example:4222", host.Services.GetRequiredService<IOptionsMonitor<NatsOptions>>().Get("orders").Url);
        }

        [Fact]
        public void ShouldConfigureOptionsInCode()
        {
            using var host = BuildHost(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service
                    .UseNats(options => options.Url = "nats://nats.example:4222")
                    .SerializedWith(new JsonMessageSerializer(), new JsonMessageDeserializerFactory(typeof(JsonMessageDeserializer<>))))));

            Assert.Equal("nats://nats.example:4222", host.Services.GetRequiredService<IOptionsMonitor<NatsOptions>>().Get("orders").Url);
        }

        [Fact]
        public async Task ShouldFailToStartWhenTheUrlIsMissingFromConfiguration()
        {
            var configuration = Configuration(new Dictionary<string, string>());
            using var host = BuildHost(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service
                    .UseNats(configuration.GetSection("Obvs:Orders:Nats"))
                    .SerializedWith(new JsonMessageSerializer(), new JsonMessageDeserializerFactory(typeof(JsonMessageDeserializer<>))))));

            var exception = await Assert.ThrowsAsync<ObvsConfigurationException>(() => host.StartAsync(TestContext.Current.CancellationToken));

            var error = Assert.Single(exception.Errors);
            Assert.Contains("'orders'", error);
            Assert.Contains("Obvs:Orders:Nats:Url", error);
        }

        [Fact]
        public async Task ShouldFailToStartWhenTheUrlIsMissingFromCode()
        {
            using var host = BuildHost(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service
                    .UseNats(options => { })
                    .SerializedWith(new JsonMessageSerializer(), new JsonMessageDeserializerFactory(typeof(JsonMessageDeserializer<>))))));

            var exception = await Assert.ThrowsAsync<ObvsConfigurationException>(() => host.StartAsync(TestContext.Current.CancellationToken));

            var error = Assert.Single(exception.Errors);
            Assert.Contains("'orders'", error);
            Assert.Contains("Url", error);
        }

        [Fact]
        public async Task ShouldFailToStartWithoutASerializer()
        {
            using var host = BuildHost(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service.UseNats(options => options.Url = "nats://nats.example:4222"))));

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
