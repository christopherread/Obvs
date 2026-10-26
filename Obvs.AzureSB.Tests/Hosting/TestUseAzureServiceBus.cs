using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Obvs.Hosting;
using Obvs.Serialization.Json;
using Xunit;

namespace Obvs.AzureSB.Tests.Hosting
{
    // Configuration and validation; none of these connect to Service Bus
    public class TestUseAzureServiceBus
    {
        [Fact]
        public void ShouldBindOptionsFromConfiguration()
        {
            var configuration = Configuration(new Dictionary<string, string> { ["Obvs:Orders:AzureServiceBus:FullyQualifiedNamespace"] = "contoso.servicebus.windows.net" });
            using var host = BuildHost(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service
                    .UseAzureServiceBus(configuration.GetSection("Obvs:Orders:AzureServiceBus"))
                    .SerializedWith(new JsonMessageSerializer(), new JsonMessageDeserializerFactory(typeof(JsonMessageDeserializer<>))))));

            Assert.Equal("contoso.servicebus.windows.net", host.Services.GetRequiredService<IOptionsMonitor<AzureServiceBusOptions>>().Get("orders").FullyQualifiedNamespace);
        }

        [Fact]
        public void ShouldConfigureOptionsInCode()
        {
            var credential = new AzureCliCredential();
            using var host = BuildHost(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service
                    .UseAzureServiceBus(options =>
                    {
                        options.FullyQualifiedNamespace = "contoso.servicebus.windows.net";
                        options.Credential = credential;
                    })
                    .SerializedWith(new JsonMessageSerializer(), new JsonMessageDeserializerFactory(typeof(JsonMessageDeserializer<>))))));

            var options = host.Services.GetRequiredService<IOptionsMonitor<AzureServiceBusOptions>>().Get("orders");
            Assert.Equal("contoso.servicebus.windows.net", options.FullyQualifiedNamespace);
            Assert.Same(credential, options.Credential);
        }

        [Fact]
        public void ShouldBindMaxDeliveryAttemptsFromConfiguration()
        {
            var configuration = Configuration(new Dictionary<string, string>
            {
                ["Obvs:Orders:AzureServiceBus:FullyQualifiedNamespace"] = "contoso.servicebus.windows.net",
                ["Obvs:Orders:AzureServiceBus:MaxDeliveryAttempts"] = "3",
            });
            using var host = BuildHost(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service
                    .UseAzureServiceBus(configuration.GetSection("Obvs:Orders:AzureServiceBus"))
                    .SerializedWith(new JsonMessageSerializer(), new JsonMessageDeserializerFactory(typeof(JsonMessageDeserializer<>))))));

            Assert.Equal(3, host.Services.GetRequiredService<IOptionsMonitor<AzureServiceBusOptions>>().Get("orders").MaxDeliveryAttempts);
        }

        [Fact]
        public void ShouldDeliverAFailingMessageTenTimesByDefault()
        {
            Assert.Equal(10, new AzureServiceBusOptions().MaxDeliveryAttempts);
        }

        [Fact]
        public async Task ShouldFailToStartWhenMaxDeliveryAttemptsIsLessThanOne()
        {
            using var host = BuildHost(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service
                    .UseAzureServiceBus(options =>
                    {
                        options.FullyQualifiedNamespace = "contoso.servicebus.windows.net";
                        options.MaxDeliveryAttempts = 0;
                    })
                    .SerializedWith(new JsonMessageSerializer(), new JsonMessageDeserializerFactory(typeof(JsonMessageDeserializer<>))))));

            var exception = await Assert.ThrowsAsync<ObvsConfigurationException>(() => host.StartAsync(TestContext.Current.CancellationToken));

            var error = Assert.Single(exception.Errors);
            Assert.Contains("'orders'", error);
            Assert.Contains("MaxDeliveryAttempts", error);
            Assert.Contains("at least 1", error);
        }

        [Fact]
        public async Task ShouldFailToStartWhenTheNamespaceIsMissingFromConfiguration()
        {
            var configuration = Configuration(new Dictionary<string, string>());
            using var host = BuildHost(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service
                    .UseAzureServiceBus(configuration.GetSection("Obvs:Orders:AzureServiceBus"))
                    .SerializedWith(new JsonMessageSerializer(), new JsonMessageDeserializerFactory(typeof(JsonMessageDeserializer<>))))));

            var exception = await Assert.ThrowsAsync<ObvsConfigurationException>(() => host.StartAsync(TestContext.Current.CancellationToken));

            var error = Assert.Single(exception.Errors);
            Assert.Contains("'orders'", error);
            Assert.Contains("Obvs:Orders:AzureServiceBus:FullyQualifiedNamespace", error);
            Assert.Contains("Obvs:Orders:AzureServiceBus:ConnectionString", error);
        }

        [Fact]
        public async Task ShouldFailToStartWhenTheNamespaceIsMissingFromCode()
        {
            using var host = BuildHost(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service
                    .UseAzureServiceBus(options => { })
                    .SerializedWith(new JsonMessageSerializer(), new JsonMessageDeserializerFactory(typeof(JsonMessageDeserializer<>))))));

            var exception = await Assert.ThrowsAsync<ObvsConfigurationException>(() => host.StartAsync(TestContext.Current.CancellationToken));

            var error = Assert.Single(exception.Errors);
            Assert.Contains("'orders'", error);
            Assert.Contains("options.FullyQualifiedNamespace", error);
            Assert.Contains("options.ConnectionString", error);
        }

        [Fact]
        public async Task ShouldFailToStartWhenBothANamespaceAndAConnectionStringAreSet()
        {
            using var host = BuildHost(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service
                    .UseAzureServiceBus(options =>
                    {
                        options.FullyQualifiedNamespace = "contoso.servicebus.windows.net";
                        options.ConnectionString = "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";
                    })
                    .SerializedWith(new JsonMessageSerializer(), new JsonMessageDeserializerFactory(typeof(JsonMessageDeserializer<>))))));

            var exception = await Assert.ThrowsAsync<ObvsConfigurationException>(() => host.StartAsync(TestContext.Current.CancellationToken));

            var error = Assert.Single(exception.Errors);
            Assert.Contains("'orders'", error);
            Assert.Contains("not both", error);
        }

        [Fact]
        public async Task ShouldFailToStartWithoutASerializer()
        {
            using var host = BuildHost(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service
                    .UseAzureServiceBus(options => options.FullyQualifiedNamespace = "contoso.servicebus.windows.net"))));

            var exception = await Assert.ThrowsAsync<ObvsConfigurationException>(() => host.StartAsync(TestContext.Current.CancellationToken));

            var error = Assert.Single(exception.Errors);
            Assert.Contains("'orders'", error);
            Assert.Contains("SerializedWith", error);
        }

        [Fact]
        public async Task ShouldFailToStartWhenTheInstanceIdIsTooLongForASubscriptionName()
        {
            // This bus's event subscription is named after its instance ID, and Service Bus allows 50 characters
            using var host = BuildHost(services => services.AddObvs(obvs => obvs
                .UseInstanceId(new string('i', 51))
                .AddService<IOrderMessage>("orders", service => service
                    .UseAzureServiceBus(options => options.FullyQualifiedNamespace = "contoso.servicebus.windows.net")
                    .SerializedWith(new JsonMessageSerializer(), new JsonMessageDeserializerFactory(typeof(JsonMessageDeserializer<>))))));

            var exception = await Assert.ThrowsAsync<ObvsConfigurationException>(() => host.StartAsync(TestContext.Current.CancellationToken));

            var error = Assert.Single(exception.Errors);
            Assert.Contains("'orders'", error);
            Assert.Contains("50 characters", error);
            Assert.Contains("UseInstanceId", error);
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
