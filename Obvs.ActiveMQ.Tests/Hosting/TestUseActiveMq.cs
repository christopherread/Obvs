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

namespace Obvs.ActiveMQ.Tests.Hosting
{
    // Configuration and validation; none of these connect to a broker
    public class TestUseActiveMq
    {
        [Fact]
        public void ShouldBindOptionsFromConfiguration()
        {
            var configuration = Configuration(new Dictionary<string, string>
            {
                ["Obvs:Orders:ActiveMq:BrokerUri"] = "activemq:tcp://activemq.example:61616",
                ["Obvs:Orders:ActiveMq:UserName"] = "orders-app",
                ["Obvs:Orders:ActiveMq:Password"] = "not-a-real-password",
            });
            using var host = BuildHost(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service
                    .UseActiveMq(configuration.GetSection("Obvs:Orders:ActiveMq"))
                    .SerializedWith(new JsonMessageSerializer(), new JsonMessageDeserializerFactory(typeof(JsonMessageDeserializer<>))))));

            var options = host.Services.GetRequiredService<IOptionsMonitor<ActiveMqOptions>>().Get("orders");
            Assert.Equal("activemq:tcp://activemq.example:61616", options.BrokerUri);
            Assert.Equal("orders-app", options.UserName);
            Assert.Equal("not-a-real-password", options.Password);
        }

        [Fact]
        public void ShouldConfigureOptionsInCode()
        {
            using var host = BuildHost(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service
                    .UseActiveMq(options => options.BrokerUri = "activemq:tcp://activemq.example:61616")
                    .SerializedWith(new JsonMessageSerializer(), new JsonMessageDeserializerFactory(typeof(JsonMessageDeserializer<>))))));

            Assert.Equal("activemq:tcp://activemq.example:61616", host.Services.GetRequiredService<IOptionsMonitor<ActiveMqOptions>>().Get("orders").BrokerUri);
        }

        [Fact]
        public async Task ShouldFailToStartWhenTheBrokerUriIsMissingFromConfiguration()
        {
            var configuration = Configuration(new Dictionary<string, string>());
            using var host = BuildHost(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service
                    .UseActiveMq(configuration.GetSection("Obvs:Orders:ActiveMq"))
                    .SerializedWith(new JsonMessageSerializer(), new JsonMessageDeserializerFactory(typeof(JsonMessageDeserializer<>))))));

            var exception = await Assert.ThrowsAsync<ObvsConfigurationException>(() => host.StartAsync(TestContext.Current.CancellationToken));

            var error = Assert.Single(exception.Errors);
            Assert.Contains("'orders'", error);
            Assert.Contains("Obvs:Orders:ActiveMq:BrokerUri", error);
        }

        [Fact]
        public async Task ShouldFailToStartWhenTheBrokerUriIsMissingFromCode()
        {
            using var host = BuildHost(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service
                    .UseActiveMq(options => { })
                    .SerializedWith(new JsonMessageSerializer(), new JsonMessageDeserializerFactory(typeof(JsonMessageDeserializer<>))))));

            var exception = await Assert.ThrowsAsync<ObvsConfigurationException>(() => host.StartAsync(TestContext.Current.CancellationToken));

            var error = Assert.Single(exception.Errors);
            Assert.Contains("'orders'", error);
            Assert.Contains("BrokerUri", error);
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
