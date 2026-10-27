using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SQS.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Obvs.Hosting;
using Xunit;

namespace Obvs.AmazonSqs.Tests.Hosting
{
    // Running buses deleting the queues and subscriptions of buses that crashed, against LocalStack
    // (see LocalStack/docker-compose.yml). Buses here refresh every second and count as abandoned after three.
    public class TestAmazonSqsCleanUp : IClassFixture<TestAmazonSqsHosting.ServiceNames>
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);
        private static readonly TimeSpan LastSeenInterval = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan AbandonedAfter = TimeSpan.FromSeconds(3);
        private readonly TestAmazonSqsHosting.ServiceNames _serviceNames;

        public TestAmazonSqsCleanUp(TestAmazonSqsHosting.ServiceNames serviceNames)
        {
            _serviceNames = serviceNames;
        }

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldTagTheBusesOwnQueuesWithWhenItWasLastSeen()
        {
            var serviceName = _serviceNames.Create("last-seen");
            const string instanceId = "worker-1";
            var before = DateTimeOffset.UtcNow.AddSeconds(-1);

            using var host = await TestAmazonSqsHosting.StartHostAsync(serviceName, instanceId: instanceId);

            foreach (var queueName in BusQueues(serviceName, instanceId))
            {
                var tags = await TestBroker.QueueTagsAsync(queueName, Ct);
                var lastSeen = DateTimeOffset.Parse(tags["obvs-last-seen"], CultureInfo.InvariantCulture);
                Assert.InRange(lastSeen, before, DateTimeOffset.UtcNow.AddSeconds(1));
                Assert.Equal("3600", tags["obvs-abandoned-after"]);
            }
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldRefreshWhenTheBusWasLastSeenWhileItRuns()
        {
            var serviceName = _serviceNames.Create("refresh");
            const string instanceId = "worker-1";
            using var host = await StartHostAsync(serviceName, instanceId);
            var first = await LastSeenAsync($"{serviceName}-events-{instanceId}");

            await EventuallyAsync(async () => await LastSeenAsync($"{serviceName}-events-{instanceId}") > first &&
                                              await LastSeenAsync($"{serviceName}-responses-{instanceId}") > first,
                "the bus to refresh when it was last seen");
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldRefreshWhenItWasLastSeenOnQueuesItReuses()
        {
            // A bus restarting with a fixed instance ID finds its queues as it left them, last seen before it stopped
            var serviceName = _serviceNames.Create("reuse-last-seen");
            const string instanceId = "worker-1";
            await CreateCrashedBusAsync(serviceName, instanceId, DateTimeOffset.UtcNow.AddHours(-2));
            var before = DateTimeOffset.UtcNow.AddSeconds(-1);

            using var host = await TestAmazonSqsHosting.StartHostAsync(serviceName, instanceId: instanceId);

            foreach (var queueName in BusQueues(serviceName, instanceId))
            {
                Assert.True(await LastSeenAsync(queueName) >= before, $"{queueName} wasn't refreshed at startup.");
            }
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldDeleteTheQueuesAndSubscriptionOfABusNotSeenWithinItsWindow()
        {
            var serviceName = _serviceNames.Create("abandoned");
            await CreateCrashedBusAsync(serviceName, "crashed", DateTimeOffset.UtcNow.AddHours(-2));

            using var host = await StartHostAsync(serviceName, "worker-1");

            await EventuallyAsync(async () => await TestBroker.QueueUrlAsync($"{serviceName}-events-crashed", Ct) == null &&
                                              await TestBroker.QueueUrlAsync($"{serviceName}-responses-crashed", Ct) == null,
                "the crashed bus's queues to be deleted");
            var eventQueueArn = (await TestBroker.QueueAttributesAsync($"{serviceName}-events-worker-1", Ct))["QueueArn"];
            var subscription = Assert.Single(await TestAmazonSqsHosting.SubscriptionsAsync(await TestAmazonSqsHosting.TopicArnAsync($"{serviceName}-events")));
            Assert.Equal(eventQueueArn, subscription.Endpoint);
            Assert.NotNull(await TestBroker.QueueUrlAsync($"{serviceName}-commands", Ct));
            Assert.NotNull(await TestBroker.QueueUrlAsync($"{serviceName}-requests", Ct));
            Assert.NotNull(await TestBroker.QueueUrlAsync($"{serviceName}-dead-letters", Ct));
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldKeepTheQueuesOfABusSeenWithinItsWindow()
        {
            // Last seen ten minutes ago, by a bus that counts as abandoned only after an hour
            var serviceName = _serviceNames.Create("seen");
            await CreateCrashedBusAsync(serviceName, "quiet", DateTimeOffset.UtcNow.AddMinutes(-10));
            await CreateCrashedBusAsync(serviceName, "crashed", DateTimeOffset.UtcNow.AddHours(-2));

            using var host = await StartHostAsync(serviceName, "worker-1");

            await WaitForASweepAsync(serviceName);
            Assert.NotNull(await TestBroker.QueueUrlAsync($"{serviceName}-events-quiet", Ct));
            Assert.NotNull(await TestBroker.QueueUrlAsync($"{serviceName}-responses-quiet", Ct));
            Assert.Equal(2, (await TestAmazonSqsHosting.SubscriptionsAsync(await TestAmazonSqsHosting.TopicArnAsync($"{serviceName}-events"))).Count);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldKeepTheQueuesOfABusThatDoesntRecordWhenItWasLastSeen()
        {
            var serviceName = _serviceNames.Create("untagged");
            await CreateCrashedBusAsync(serviceName, "untagged", lastSeen: null);
            await CreateCrashedBusAsync(serviceName, "crashed", DateTimeOffset.UtcNow.AddHours(-2));

            using var host = await StartHostAsync(serviceName, "worker-1");

            await WaitForASweepAsync(serviceName);
            Assert.NotNull(await TestBroker.QueueUrlAsync($"{serviceName}-events-untagged", Ct));
            Assert.NotNull(await TestBroker.QueueUrlAsync($"{serviceName}-responses-untagged", Ct));
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldNeitherRecordWhenItWasLastSeenNorCleanUpWhenTurnedOff()
        {
            var serviceName = _serviceNames.Create("turned-off");
            await CreateCrashedBusAsync(serviceName, "crashed", DateTimeOffset.UtcNow.AddHours(-2));

            using var host = await StartHostAsync(serviceName, "worker-1", options => options.CleanUpAbandonedQueues = false);
            await Task.Delay(AbandonedAfter + LastSeenInterval, Ct);

            Assert.NotNull(await TestBroker.QueueUrlAsync($"{serviceName}-events-crashed", Ct));
            Assert.NotNull(await TestBroker.QueueUrlAsync($"{serviceName}-responses-crashed", Ct));
            foreach (var queueName in BusQueues(serviceName, "worker-1"))
            {
                var tags = await TestBroker.QueueTagsAsync(queueName, Ct);
                Assert.Equal("worker-1", tags["obvs-instance-id"]);
                Assert.False(tags.ContainsKey("obvs-last-seen"));
                Assert.False(tags.ContainsKey("obvs-abandoned-after"));
            }
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldRecreateItsQueuesAndSubscriptionIfTheyreDeletedWhileItRuns()
        {
            // As another bus would if this one went unseen for longer than its window, for example while paused in a
            // debugger or on a suspended machine
            var serviceName = _serviceNames.Create("recreate");
            const string instanceId = "worker-1";
            using var host = await StartHostAsync(serviceName, instanceId, configure: service => service
                .HandleRequest<GetQuote, Quote>((request, context, ct) => Task.FromResult(new Quote { Price = 9.99m })));
            var bus = host.Services.GetRequiredService<IServiceBus>();
            var received = bus.Events.OfType<OrderPlaced>().Where(placed => placed.OrderId == "o-2").FirstAsync().ToTask();

            await DeleteBusEntitiesAsync(serviceName, instanceId);
            await EventuallyAsync(async () => await TestBroker.QueueUrlAsync($"{serviceName}-events-{instanceId}", Ct) != null &&
                                              await TestBroker.QueueUrlAsync($"{serviceName}-responses-{instanceId}", Ct) != null &&
                                              (await TestAmazonSqsHosting.SubscriptionsAsync(await TestAmazonSqsHosting.TopicArnAsync($"{serviceName}-events"))).Count == 1,
                "the bus to recreate its queues and subscription");
            await bus.PublishAsync(new OrderPlaced { OrderId = "o-2" }, TestContext.Current.CancellationToken);

            Assert.Equal("o-2", (await received.WaitAsync(Timeout, Ct)).OrderId);
            var quote = await host.Services.GetRequiredService<IServiceBusClient>().RequestAsync<GetQuote, Quote>(new GetQuote { Sku = "sku-1" }, Timeout, Ct);
            Assert.Equal(9.99m, quote.Price);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldNotRecreateItsQueuesAfterItStops()
        {
            var serviceName = _serviceNames.Create("stopped");
            const string instanceId = "worker-1";
            var host = await StartHostAsync(serviceName, instanceId);

            await host.StopAsync(Ct);
            host.Dispose();
            await Task.Delay(LastSeenInterval + LastSeenInterval, Ct);

            Assert.Null(await TestBroker.QueueUrlAsync($"{serviceName}-events-{instanceId}", Ct));
            Assert.Null(await TestBroker.QueueUrlAsync($"{serviceName}-responses-{instanceId}", Ct));
            Assert.Empty(await TestAmazonSqsHosting.SubscriptionsAsync(await TestAmazonSqsHosting.TopicArnAsync($"{serviceName}-events")));
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldLetSeveralBusesCleanUpAtOnce()
        {
            var serviceName = _serviceNames.Create("sweep-together");
            await CreateCrashedBusAsync(serviceName, "crashed-1", DateTimeOffset.UtcNow.AddHours(-2));
            await CreateCrashedBusAsync(serviceName, "crashed-2", DateTimeOffset.UtcNow.AddHours(-2));
            var topicArn = await TestAmazonSqsHosting.TopicArnAsync($"{serviceName}-events");

            // Every live bus sweeps, and these sweeps race them for the same queues and subscriptions
            var hostsStarting = Task.WhenAll(new[] { "worker-1", "worker-2", "worker-3" }.Select(instanceId => StartHostAsync(serviceName, instanceId)));
            using var sqs = TestBroker.CreateSqsClient();
            using var sns = TestBroker.CreateSnsClient();
            await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => AbandonedQueues.SweepAsync(sqs, sns, serviceName, "sweeper", topicArn, Ct)));
            var hosts = await hostsStarting;
            try
            {
                await EventuallyAsync(async () => (await QueueNamesAsync(serviceName)).All(name => !name.Contains("crashed")), "the crashed buses' queues to be deleted");

                // Past the window, so a live bus deleted by mistake would be gone by now
                await Task.Delay(AbandonedAfter + LastSeenInterval, Ct);
                foreach (var instanceId in new[] { "worker-1", "worker-2", "worker-3" })
                {
                    Assert.NotNull(await TestBroker.QueueUrlAsync($"{serviceName}-events-{instanceId}", Ct));
                    Assert.NotNull(await TestBroker.QueueUrlAsync($"{serviceName}-responses-{instanceId}", Ct));
                }
                Assert.Equal(3, (await TestAmazonSqsHosting.SubscriptionsAsync(topicArn)).Count);

                var received = hosts.Select(host => host.Services.GetRequiredService<IServiceBus>().Events.OfType<OrderPlaced>().FirstAsync().ToTask()).ToList();
                await hosts[0].Services.GetRequiredService<IServiceBus>().PublishAsync(new OrderPlaced { OrderId = "o-1" }, TestContext.Current.CancellationToken);
                foreach (var placed in received)
                {
                    Assert.Equal("o-1", (await placed.WaitAsync(Timeout, Ct)).OrderId);
                }
            }
            finally
            {
                foreach (var host in hosts)
                {
                    host.Dispose();
                }
            }
        }

        private static Task<IHost> StartHostAsync(string serviceName, string instanceId, Action<AmazonSqsOptions> configureOptions = null,
            Action<ObvsServiceBuilder> configure = null)
        {
            return TestAmazonSqsHosting.StartHostAsync(serviceName, configure, instanceId, options =>
            {
                options.LastSeenInterval = LastSeenInterval;
                options.AbandonedAfter = AbandonedAfter;
                configureOptions?.Invoke(options);
            });
        }

        private static IEnumerable<string> BusQueues(string serviceName, string instanceId)
        {
            return new[] { $"{serviceName}-events-{instanceId}", $"{serviceName}-responses-{instanceId}" };
        }

        // The queues and subscription a bus that has since crashed left behind, last seen when given and counting as
        // abandoned after an hour. With no time given, the queues are tagged as a bus with clean-up turned off tags them.
        private static async Task CreateCrashedBusAsync(string serviceName, string instanceId, DateTimeOffset? lastSeen)
        {
            var options = new AmazonSqsOptions { CleanUpAbandonedQueues = false };
            TestBroker.Configure(options);
            await new AmazonSqsEntities(serviceName, instanceId, options).EnsureCreatedAsync(true);
            if (lastSeen == null)
            {
                return;
            }

            using var sqs = TestBroker.CreateSqsClient();
            foreach (var queueName in BusQueues(serviceName, instanceId))
            {
                await sqs.TagQueueAsync(new TagQueueRequest
                {
                    QueueUrl = await TestBroker.QueueUrlAsync(queueName, Ct),
                    Tags = new Dictionary<string, string>
                    {
                        ["obvs-last-seen"] = lastSeen.Value.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
                        ["obvs-abandoned-after"] = "3600",
                    },
                }, Ct);
            }
        }

        // What a sweeping bus deletes
        private static async Task DeleteBusEntitiesAsync(string serviceName, string instanceId)
        {
            var eventQueueArn = (await TestBroker.QueueAttributesAsync($"{serviceName}-events-{instanceId}", Ct))["QueueArn"];
            using var sns = TestBroker.CreateSnsClient();
            foreach (var subscription in await TestAmazonSqsHosting.SubscriptionsAsync(await TestAmazonSqsHosting.TopicArnAsync($"{serviceName}-events")))
            {
                if (subscription.Endpoint == eventQueueArn)
                {
                    await sns.UnsubscribeAsync(subscription.SubscriptionArn, Ct);
                }
            }
            using var sqs = TestBroker.CreateSqsClient();
            foreach (var queueName in BusQueues(serviceName, instanceId))
            {
                await sqs.DeleteQueueAsync(await TestBroker.QueueUrlAsync(queueName, Ct), Ct);
            }
        }

        // Once the crashed bus's queues are gone, the running bus has swept at least once
        private static Task WaitForASweepAsync(string serviceName)
        {
            return EventuallyAsync(async () => await TestBroker.QueueUrlAsync($"{serviceName}-events-crashed", Ct) == null &&
                                               await TestBroker.QueueUrlAsync($"{serviceName}-responses-crashed", Ct) == null,
                "the running bus to sweep");
        }

        private static async Task<DateTimeOffset> LastSeenAsync(string queueName)
        {
            return DateTimeOffset.Parse((await TestBroker.QueueTagsAsync(queueName, Ct))["obvs-last-seen"], CultureInfo.InvariantCulture);
        }

        private static async Task<List<string>> QueueNamesAsync(string serviceName)
        {
            using var sqs = TestBroker.CreateSqsClient();
            var queues = await sqs.ListQueuesAsync(new ListQueuesRequest { QueueNamePrefix = serviceName }, Ct);
            return (queues.QueueUrls ?? new List<string>()).Select(url => url.Substring(url.LastIndexOf('/') + 1)).ToList();
        }

        private static async Task EventuallyAsync(Func<Task<bool>> condition, string what)
        {
            var deadline = DateTime.UtcNow + Timeout;
            while (!await condition())
            {
                if (DateTime.UtcNow > deadline)
                {
                    throw new TimeoutException($"Timed out waiting for {what}.");
                }
                await Task.Delay(200, Ct);
            }
        }
    }
}
