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

namespace Obvs.RabbitMQ.Tests.Hosting
{
    // Configuration and validation; none of these connect to a broker
    public class TestUseRabbitMq
    {
        [Fact]
        public void ShouldBindOptionsFromConfiguration()
        {
            var configuration = Configuration(new Dictionary<string, string>
            {
                ["Obvs:Orders:RabbitMq:Uri"] = "amqp://broker.example:5672",
                ["Obvs:Orders:RabbitMq:AutomaticRecoveryEnabled"] = "false",
            });
            using var host = BuildHost(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service
                    .UseRabbitMq(configuration.GetSection("Obvs:Orders:RabbitMq"))
                    .SerializedWith(new JsonMessageSerializer(), new JsonMessageDeserializerFactory(typeof(JsonMessageDeserializer<>))))));

            var options = host.Services.GetRequiredService<IOptionsMonitor<RabbitMqOptions>>().Get("orders");

            Assert.Equal(new Uri("amqp://broker.example:5672"), options.Uri);
            Assert.False(options.AutomaticRecoveryEnabled);
        }

        [Fact]
        public void ShouldConfigureOptionsInCode()
        {
            using var host = BuildHost(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service
                    .UseRabbitMq(options => options.Uri = new Uri("amqp://broker.example:5672"))
                    .SerializedWith(new JsonMessageSerializer(), new JsonMessageDeserializerFactory(typeof(JsonMessageDeserializer<>))))));

            var options = host.Services.GetRequiredService<IOptionsMonitor<RabbitMqOptions>>().Get("orders");

            Assert.Equal(new Uri("amqp://broker.example:5672"), options.Uri);
            Assert.True(options.AutomaticRecoveryEnabled);
        }

        [Fact]
        public async Task ShouldFailToStartWhenTheUriIsMissingFromConfiguration()
        {
            var configuration = Configuration(new Dictionary<string, string>());
            using var host = BuildHost(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service
                    .UseRabbitMq(configuration.GetSection("Obvs:Orders:RabbitMq"))
                    .SerializedWith(new JsonMessageSerializer(), new JsonMessageDeserializerFactory(typeof(JsonMessageDeserializer<>))))));

            var exception = await Assert.ThrowsAsync<ObvsConfigurationException>(
                () => host.StartAsync(TestContext.Current.CancellationToken));

            var error = Assert.Single(exception.Errors);
            Assert.Contains("'orders'", error);
            Assert.Contains("Obvs:Orders:RabbitMq:Uri", error);
        }

        [Fact]
        public async Task ShouldFailToStartWhenTheUriIsMissingFromCode()
        {
            using var host = BuildHost(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service
                    .UseRabbitMq(options => { })
                    .SerializedWith(new JsonMessageSerializer(), new JsonMessageDeserializerFactory(typeof(JsonMessageDeserializer<>))))));

            var exception = await Assert.ThrowsAsync<ObvsConfigurationException>(
                () => host.StartAsync(TestContext.Current.CancellationToken));

            var error = Assert.Single(exception.Errors);
            Assert.Contains("'orders'", error);
            Assert.Contains("Uri", error);
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
