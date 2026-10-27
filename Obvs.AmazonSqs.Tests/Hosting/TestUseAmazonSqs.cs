using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Obvs.Hosting;
using Obvs.Serialization.Json;
using Xunit;

namespace Obvs.AmazonSqs.Tests.Hosting
{
    // Configuration and validation; none of these connect to AWS
    public class TestUseAmazonSqs
    {
        [Fact]
        public void ShouldBindOptionsFromConfiguration()
        {
            var configuration = Configuration(new Dictionary<string, string>
            {
                ["Obvs:Orders:AmazonSqs:Region"] = "eu-west-1",
                ["Obvs:Orders:AmazonSqs:ServiceUrl"] = "http://localhost:4566",
            });
            using var host = BuildHost(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service
                    .UseAmazonSqs(configuration.GetSection("Obvs:Orders:AmazonSqs"))
                    .SerializedWith(new JsonMessageSerializer(), new JsonMessageDeserializerFactory(typeof(JsonMessageDeserializer<>))))));

            var options = host.Services.GetRequiredService<IOptionsMonitor<AmazonSqsOptions>>().Get("orders");
            Assert.Equal("eu-west-1", options.Region);
            Assert.Equal("http://localhost:4566", options.ServiceUrl);
        }

        [Fact]
        public void ShouldConfigureOptionsInCode()
        {
            var credentials = new BasicAWSCredentials("access-key", "secret-key");
            using var host = BuildHost(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service
                    .UseAmazonSqs(options =>
                    {
                        options.Region = "eu-west-1";
                        options.Credentials = credentials;
                    })
                    .SerializedWith(new JsonMessageSerializer(), new JsonMessageDeserializerFactory(typeof(JsonMessageDeserializer<>))))));

            var options = host.Services.GetRequiredService<IOptionsMonitor<AmazonSqsOptions>>().Get("orders");
            Assert.Equal("eu-west-1", options.Region);
            Assert.Same(credentials, options.Credentials);
        }

        [Fact]
        public void ShouldKeepEachServicesOptionsApart()
        {
            using var host = BuildHost(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service
                    .UseAmazonSqs(options => options.Region = "eu-west-1")
                    .SerializedWith(new JsonMessageSerializer(), new JsonMessageDeserializerFactory(typeof(JsonMessageDeserializer<>))))
                .AddService<IOrderMessage>("billing", service => service
                    .UseAmazonSqs(options => options.Region = "us-east-2")
                    .SerializedWith(new JsonMessageSerializer(), new JsonMessageDeserializerFactory(typeof(JsonMessageDeserializer<>))))));

            var options = host.Services.GetRequiredService<IOptionsMonitor<AmazonSqsOptions>>();
            Assert.Equal("eu-west-1", options.Get("orders").Region);
            Assert.Equal("us-east-2", options.Get("billing").Region);
        }

        [Fact]
        public void ShouldBindDeliverySettingsFromConfiguration()
        {
            var configuration = Configuration(new Dictionary<string, string>
            {
                ["Obvs:Orders:AmazonSqs:Region"] = "eu-west-1",
                ["Obvs:Orders:AmazonSqs:MaxDeliveryAttempts"] = "3",
                ["Obvs:Orders:AmazonSqs:MaxConcurrentCalls"] = "4",
                ["Obvs:Orders:AmazonSqs:VisibilityTimeout"] = "00:05:00",
                ["Obvs:Orders:AmazonSqs:MaxHandlerDuration"] = "00:04:00",
            });
            using var host = BuildHost(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service
                    .UseAmazonSqs(configuration.GetSection("Obvs:Orders:AmazonSqs"))
                    .SerializedWith(new JsonMessageSerializer(), new JsonMessageDeserializerFactory(typeof(JsonMessageDeserializer<>))))));

            var options = host.Services.GetRequiredService<IOptionsMonitor<AmazonSqsOptions>>().Get("orders");
            Assert.Equal(3, options.MaxDeliveryAttempts);
            Assert.Equal(4, options.MaxConcurrentCalls);
            Assert.Equal(TimeSpan.FromMinutes(5), options.VisibilityTimeout);
            Assert.Equal(TimeSpan.FromMinutes(4), options.MaxHandlerDuration);
        }

        [Fact]
        public void ShouldDeliverAFailingMessageTenTimesOneMessageAtATimeByDefault()
        {
            var options = new AmazonSqsOptions();

            Assert.Equal(10, options.MaxDeliveryAttempts);
            Assert.Equal(1, options.MaxConcurrentCalls);
            Assert.True(options.VisibilityTimeout > options.MaxHandlerDuration);
            Assert.Null(options.ServiceUrl);
            Assert.Null(options.Credentials);
        }

        [Fact]
        public void ShouldCleanUpQueuesNotSeenForAnHourRefreshingEveryFiveMinutesByDefault()
        {
            var options = new AmazonSqsOptions();

            Assert.True(options.CleanUpAbandonedQueues);
            Assert.Equal(TimeSpan.FromMinutes(5), options.LastSeenInterval);
            Assert.Equal(TimeSpan.FromHours(1), options.AbandonedAfter);
        }

        [Fact]
        public void ShouldBindCleanUpSettingsFromConfiguration()
        {
            var configuration = Configuration(new Dictionary<string, string>
            {
                ["Obvs:Orders:AmazonSqs:Region"] = "eu-west-1",
                ["Obvs:Orders:AmazonSqs:CleanUpAbandonedQueues"] = "false",
                ["Obvs:Orders:AmazonSqs:LastSeenInterval"] = "00:10:00",
                ["Obvs:Orders:AmazonSqs:AbandonedAfter"] = "1.00:00:00",
            });
            using var host = BuildHost(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service
                    .UseAmazonSqs(configuration.GetSection("Obvs:Orders:AmazonSqs"))
                    .SerializedWith(new JsonMessageSerializer(), new JsonMessageDeserializerFactory(typeof(JsonMessageDeserializer<>))))));

            var options = host.Services.GetRequiredService<IOptionsMonitor<AmazonSqsOptions>>().Get("orders");
            Assert.False(options.CleanUpAbandonedQueues);
            Assert.Equal(TimeSpan.FromMinutes(10), options.LastSeenInterval);
            Assert.Equal(TimeSpan.FromDays(1), options.AbandonedAfter);
        }

        [Fact]
        public async Task ShouldFailToStartWhenTheLastSeenIntervalIsntPositive()
        {
            var error = Assert.Single(await StartupErrorsAsync(options => options.LastSeenInterval = TimeSpan.Zero));

            Assert.Contains("'orders'", error);
            Assert.Contains("LastSeenInterval", error);
            Assert.Contains("positive", error);
        }

        [Fact]
        public async Task ShouldFailToStartWhenABusCouldBeTakenForAbandonedAfterMissingTwoRefreshes()
        {
            // A refresh that fails or runs late mustn't get a live bus's queues deleted
            var error = Assert.Single(await StartupErrorsAsync(options =>
            {
                options.LastSeenInterval = TimeSpan.FromMinutes(10);
                options.AbandonedAfter = TimeSpan.FromMinutes(29);
            }));

            Assert.Contains("'orders'", error);
            Assert.Contains("AbandonedAfter", error);
            Assert.Contains("LastSeenInterval", error);
            Assert.Contains("three times", error);
        }

        [Fact]
        public async Task ShouldNotCheckTheCleanUpSettingsWhenCleanUpIsOff()
        {
            using var host = BuildHost(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service
                    .UseAmazonSqs(options =>
                    {
                        options.Region = "eu-west-1";
                        options.CleanUpAbandonedQueues = false;
                        options.LastSeenInterval = TimeSpan.Zero;
                        options.AbandonedAfter = TimeSpan.Zero;
                        // Fails startup with the one error below rather than connecting to AWS
                        options.MaxConcurrentCalls = 0;
                    })
                    .SerializedWith(new JsonMessageSerializer(), new JsonMessageDeserializerFactory(typeof(JsonMessageDeserializer<>))))));

            var exception = await Assert.ThrowsAsync<ObvsConfigurationException>(() => host.StartAsync(TestContext.Current.CancellationToken));

            Assert.Contains("MaxConcurrentCalls", Assert.Single(exception.Errors));
        }

        [Fact]
        public async Task ShouldFailToStartWhenTheRegionIsMissingFromConfiguration()
        {
            var configuration = Configuration(new Dictionary<string, string>());
            using var host = BuildHost(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service
                    .UseAmazonSqs(configuration.GetSection("Obvs:Orders:AmazonSqs"))
                    .SerializedWith(new JsonMessageSerializer(), new JsonMessageDeserializerFactory(typeof(JsonMessageDeserializer<>))))));

            var exception = await Assert.ThrowsAsync<ObvsConfigurationException>(() => host.StartAsync(TestContext.Current.CancellationToken));

            var error = Assert.Single(exception.Errors);
            Assert.Contains("'orders'", error);
            Assert.Contains("Obvs:Orders:AmazonSqs:Region", error);
        }

        [Fact]
        public async Task ShouldFailToStartWhenTheRegionIsMissingFromCode()
        {
            var error = Assert.Single(await StartupErrorsAsync(options => options.Region = null));

            Assert.Contains("'orders'", error);
            Assert.Contains("options.Region", error);
            Assert.Contains("UseAmazonSqs", error);
        }

        [Fact]
        public async Task ShouldFailToStartWhenTheServiceUrlIsntAnAbsoluteUrl()
        {
            var error = Assert.Single(await StartupErrorsAsync(options => options.ServiceUrl = "localhost:4566"));

            Assert.Contains("'orders'", error);
            Assert.Contains("ServiceUrl", error);
            Assert.Contains("http://localhost:4566", error);
        }

        [Fact]
        public async Task ShouldFailToStartWithoutASerializer()
        {
            using var host = BuildHost(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service
                    .UseAmazonSqs(options => options.Region = "eu-west-1"))));

            var exception = await Assert.ThrowsAsync<ObvsConfigurationException>(() => host.StartAsync(TestContext.Current.CancellationToken));

            var error = Assert.Single(exception.Errors);
            Assert.Contains("'orders'", error);
            Assert.Contains("SerializedWith", error);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1001)]
        public async Task ShouldFailToStartWhenMaxDeliveryAttemptsIsOutsideWhatSqsAllows(int maxDeliveryAttempts)
        {
            var error = Assert.Single(await StartupErrorsAsync(options => options.MaxDeliveryAttempts = maxDeliveryAttempts));

            Assert.Contains("'orders'", error);
            Assert.Contains("MaxDeliveryAttempts", error);
            Assert.Contains("1 and 1000", error);
        }

        [Fact]
        public async Task ShouldFailToStartWhenMaxConcurrentCallsIsLessThanOne()
        {
            var error = Assert.Single(await StartupErrorsAsync(options => options.MaxConcurrentCalls = 0));

            Assert.Contains("'orders'", error);
            Assert.Contains("MaxConcurrentCalls", error);
            Assert.Contains("at least 1", error);
        }

        [Fact]
        public async Task ShouldFailToStartWhenTheVisibilityTimeoutDoesntExceedTheHandlerDuration()
        {
            // SQS delivers a message again once its visibility timeout passes, even while a handler is still running it
            var error = Assert.Single(await StartupErrorsAsync(options =>
            {
                options.VisibilityTimeout = TimeSpan.FromSeconds(30);
                options.MaxHandlerDuration = TimeSpan.FromSeconds(30);
            }));

            Assert.Contains("'orders'", error);
            Assert.Contains("VisibilityTimeout", error);
            Assert.Contains("MaxHandlerDuration", error);
            Assert.Contains("exceed", error);
        }

        [Theory]
        [InlineData("00:00:00.500")]
        [InlineData("12:00:01")]
        public async Task ShouldFailToStartWhenTheVisibilityTimeoutIsOutsideWhatSqsAllows(string visibilityTimeout)
        {
            var errors = await StartupErrorsAsync(options =>
            {
                options.VisibilityTimeout = TimeSpan.Parse(visibilityTimeout);
                options.MaxHandlerDuration = TimeSpan.FromMilliseconds(100);
            });

            var error = Assert.Single(errors);
            Assert.Contains("'orders'", error);
            Assert.Contains("VisibilityTimeout", error);
            Assert.Contains("1 second and 12 hours", error);
        }

        [Fact]
        public async Task ShouldFailToStartWhenMaxHandlerDurationIsntPositive()
        {
            var error = Assert.Single(await StartupErrorsAsync(options => options.MaxHandlerDuration = TimeSpan.Zero));

            Assert.Contains("'orders'", error);
            Assert.Contains("MaxHandlerDuration", error);
        }

        [Fact]
        public async Task ShouldFailToStartWhenTheServiceNameHasCharactersSqsDoesntAllow()
        {
            using var host = BuildHost(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders.v1", service => service
                    .UseAmazonSqs(options => options.Region = "eu-west-1")
                    .SerializedWith(new JsonMessageSerializer(), new JsonMessageDeserializerFactory(typeof(JsonMessageDeserializer<>))))));

            var exception = await Assert.ThrowsAsync<ObvsConfigurationException>(() => host.StartAsync(TestContext.Current.CancellationToken));

            var error = Assert.Single(exception.Errors);
            Assert.Contains("'orders.v1'", error);
            Assert.Contains("letters, digits, hyphens and underscores", error);
        }

        [Fact]
        public async Task ShouldFailToStartWhenTheInstanceIdHasCharactersSqsDoesntAllow()
        {
            using var host = BuildHost(services => services.AddObvs(obvs => obvs
                .UseInstanceId("worker.1")
                .AddService<IOrderMessage>("orders", service => service
                    .UseAmazonSqs(options => options.Region = "eu-west-1")
                    .SerializedWith(new JsonMessageSerializer(), new JsonMessageDeserializerFactory(typeof(JsonMessageDeserializer<>))))));

            var exception = await Assert.ThrowsAsync<ObvsConfigurationException>(() => host.StartAsync(TestContext.Current.CancellationToken));

            var error = Assert.Single(exception.Errors);
            Assert.Contains("'worker.1'", error);
            Assert.Contains("letters, digits, hyphens and underscores", error);
            Assert.Contains("UseInstanceId", error);
        }

        [Fact]
        public async Task ShouldFailToStartWhenAQueueNameWouldBeTooLong()
        {
            // This bus's reply queue is named {service}-responses-{instance ID}, and SQS allows 80 characters: here, 81
            using var host = BuildHost(services => services.AddObvs(obvs => obvs
                .UseInstanceId(new string('i', 64))
                .AddService<IOrderMessage>("orders", service => service
                    .UseAmazonSqs(options => options.Region = "eu-west-1")
                    .SerializedWith(new JsonMessageSerializer(), new JsonMessageDeserializerFactory(typeof(JsonMessageDeserializer<>))))));

            var exception = await Assert.ThrowsAsync<ObvsConfigurationException>(() => host.StartAsync(TestContext.Current.CancellationToken));

            var error = Assert.Single(exception.Errors);
            Assert.Contains("'orders'", error);
            Assert.Contains("80 characters", error);
            Assert.Contains($"orders-responses-{new string('i', 64)}", error);
            Assert.Contains("UseInstanceId", error);
        }

        // Starts a host whose options are valid apart from what configure sets, and returns the startup errors
        private static async Task<IReadOnlyList<string>> StartupErrorsAsync(Action<AmazonSqsOptions> configure)
        {
            using var host = BuildHost(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service
                    .UseAmazonSqs(options =>
                    {
                        options.Region = "eu-west-1";
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
