using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Obvs.Hosting;
using Xunit;

namespace Obvs.NetMQ.Tests.Hosting
{
    // Configuration and validation; none of these bind or connect a socket
    public class TestUseNetMq
    {
        [Fact]
        public void ShouldBindOptionsFromConfiguration()
        {
            var configuration = Configuration(new Dictionary<string, string>
            {
                ["Obvs:Orders:NetMq:Address"] = "tcp://orders.example",
                ["Obvs:Orders:NetMq:Port"] = "5555",
                ["Obvs:Orders:NetMq:SendHeaders"] = "true",
            });
            using var host = BuildHost(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service.UseNetMq(configuration.GetSection("Obvs:Orders:NetMq")))));

            var options = host.Services.GetRequiredService<IOptionsMonitor<NetMqOptions>>().Get("orders");
            Assert.Equal("tcp://orders.example", options.Address);
            Assert.Equal(5555, options.Port);
            Assert.True(options.SendHeaders);
        }

        [Fact]
        public void ShouldConfigureOptionsInCode()
        {
            using var host = BuildHost(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service.UseNetMq(options =>
                {
                    options.Address = "tcp://orders.example";
                    options.Port = 5555;
                }))));

            var options = host.Services.GetRequiredService<IOptionsMonitor<NetMqOptions>>().Get("orders");
            Assert.Equal("tcp://orders.example", options.Address);
            Assert.Equal(5555, options.Port);
        }

        [Fact]
        public void ShouldNotSendHeadersUnlessAsked()
        {
            using var host = BuildHost(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service.UseNetMq(options =>
                {
                    options.Address = "tcp://orders.example";
                    options.Port = 5555;
                }))));

            // v6 apps can't read a message with headers
            Assert.False(host.Services.GetRequiredService<IOptionsMonitor<NetMqOptions>>().Get("orders").SendHeaders);
        }

        [Fact]
        public async Task ShouldFailToStartWhenTheAddressIsMissingFromConfiguration()
        {
            var configuration = Configuration(new Dictionary<string, string> { ["Obvs:Orders:NetMq:Port"] = "5555" });
            using var host = BuildHost(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service.UseNetMq(configuration.GetSection("Obvs:Orders:NetMq")))));

            var exception = await Assert.ThrowsAsync<ObvsConfigurationException>(() => host.StartAsync(TestContext.Current.CancellationToken));

            var error = Assert.Single(exception.Errors);
            Assert.Contains("'orders'", error);
            Assert.Contains("Obvs:Orders:NetMq:Address", error);
        }

        [Fact]
        public async Task ShouldFailToStartWhenThePortIsMissingFromCode()
        {
            using var host = BuildHost(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service.UseNetMq(options => options.Address = "tcp://orders.example"))));

            var exception = await Assert.ThrowsAsync<ObvsConfigurationException>(() => host.StartAsync(TestContext.Current.CancellationToken));

            var error = Assert.Single(exception.Errors);
            Assert.Contains("'orders'", error);
            Assert.Contains("options.Port in UseNetMq(...)", error);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(65533)]
        public async Task ShouldFailToStartWhenItsFourPortsDontFit(int port)
        {
            using var host = BuildHost(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service.UseNetMq(options =>
                {
                    options.Address = "tcp://orders.example";
                    options.Port = port;
                }))));

            var exception = await Assert.ThrowsAsync<ObvsConfigurationException>(() => host.StartAsync(TestContext.Current.CancellationToken));

            // The service uses Port to Port + 3
            var error = Assert.Single(exception.Errors);
            Assert.Contains("'orders'", error);
            Assert.Contains("1 to 65532", error);
        }

        [Fact]
        public void ShouldNameTheTransportInTheContract()
        {
            using var host = BuildHost(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service
                    .AddMessages(typeof(PlaceOrder))
                    .UseNetMq(options =>
                    {
                        options.Address = "tcp://orders.example";
                        options.Port = 5555;
                    }))));

            var document = JsonNode.Parse(host.Services.GetRequiredService<ObvsContracts>().Describe());

            Assert.Equal("NetMQ", (string)document["channels"].AsObject().Single().Value["x-obvs-transport"]);
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
