using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Obvs.Hosting;
using Obvs.Serialization.Json;
using Xunit;

namespace Obvs.AmazonSqs.Tests.Hosting
{
    // End to end through AddObvs and UseAmazonSqs against LocalStack (see LocalStack/docker-compose.yml).
    // Service names are unique to each run, so every run creates its entities, and the fixture deletes them afterwards.
    public class TestAmazonSqsHosting : IClassFixture<TestAmazonSqsHosting.ServiceNames>
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);
        private readonly ServiceNames _serviceNames;

        public TestAmazonSqsHosting(ServiceNames serviceNames)
        {
            _serviceNames = serviceNames;
        }

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldCreateTheServicesEntitiesAndTheBusesOwn()
        {
            var serviceName = _serviceNames.Create("entities");
            const string instanceId = "orders-worker-1";

            using var host = await StartHostAsync(serviceName, instanceId: instanceId);

            Assert.NotNull(await TestBroker.QueueUrlAsync($"{serviceName}-commands", Ct));
            Assert.NotNull(await TestBroker.QueueUrlAsync($"{serviceName}-requests", Ct));
            Assert.NotNull(await TestBroker.QueueUrlAsync($"{serviceName}-dead-letters", Ct));
            Assert.NotNull(await TestBroker.QueueUrlAsync($"{serviceName}-events-{instanceId}", Ct));
            Assert.NotNull(await TestBroker.QueueUrlAsync($"{serviceName}-responses-{instanceId}", Ct));
            var topicArn = await TopicArnAsync($"{serviceName}-events");
            Assert.NotNull(topicArn);

            // Raw message delivery, so the event queue receives the body and message attributes as published
            var eventQueueArn = (await TestBroker.QueueAttributesAsync($"{serviceName}-events-{instanceId}", Ct))["QueueArn"];
            var subscription = Assert.Single(await SubscriptionsAsync(topicArn));
            Assert.Equal(eventQueueArn, subscription.Endpoint);
            Assert.Equal("sqs", subscription.Protocol);
            using var sns = TestBroker.CreateSnsClient();
            var subscriptionAttributes = (await sns.GetSubscriptionAttributesAsync(subscription.SubscriptionArn, Ct)).Attributes;
            Assert.Equal("true", subscriptionAttributes["RawMessageDelivery"]);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldTagTheBusesOwnQueuesWithItsInstanceId()
        {
            var serviceName = _serviceNames.Create("tags");
            const string instanceId = "orders-worker-1";

            using var host = await StartHostAsync(serviceName, instanceId: instanceId);

            Assert.Equal(instanceId, (await TestBroker.QueueTagsAsync($"{serviceName}-events-{instanceId}", Ct))["obvs-instance-id"]);
            Assert.Equal(instanceId, (await TestBroker.QueueTagsAsync($"{serviceName}-responses-{instanceId}", Ct))["obvs-instance-id"]);
            Assert.Equal(serviceName, (await TestBroker.QueueTagsAsync($"{serviceName}-responses-{instanceId}", Ct))["obvs-service"]);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldDeleteTheBusesOwnEntitiesWhenItStops()
        {
            var serviceName = _serviceNames.Create("cleanup");
            const string instanceId = "orders-worker-1";
            var host = await StartHostAsync(serviceName, instanceId: instanceId);

            await host.StopAsync(Ct);
            host.Dispose();

            Assert.Null(await TestBroker.QueueUrlAsync($"{serviceName}-events-{instanceId}", Ct));
            Assert.Null(await TestBroker.QueueUrlAsync($"{serviceName}-responses-{instanceId}", Ct));
            Assert.Empty(await SubscriptionsAsync(await TopicArnAsync($"{serviceName}-events")));
            Assert.NotNull(await TestBroker.QueueUrlAsync($"{serviceName}-commands", Ct));
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldReuseTheEntitiesAlreadyThere()
        {
            // A bus restarting with a stable instance ID finds its own queues and subscription
            var serviceName = _serviceNames.Create("reuse");
            const string instanceId = "orders-worker-1";
            using var first = await StartHostAsync(serviceName, instanceId: instanceId);

            using var second = await StartHostAsync(serviceName, instanceId: instanceId);

            Assert.Single(await SubscriptionsAsync(await TopicArnAsync($"{serviceName}-events")));
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldSendCommandsAndPublishEventsOverAmazonSqs()
        {
            using var host = await StartHostAsync(_serviceNames.Create("commands-events"));
            var bus = host.Services.GetRequiredService<IServiceBus>();

            var command = bus.Commands.OfType<PlaceOrder>().FirstAsync().ToTask();
            await bus.SendAsync(new PlaceOrder { OrderId = "o-1" });
            Assert.Equal("o-1", (await command.WaitAsync(Timeout, Ct)).OrderId);

            var placed = bus.Events.OfType<OrderPlaced>().FirstAsync().ToTask();
            await bus.PublishAsync(new OrderPlaced { OrderId = "o-1" });
            Assert.Equal("o-1", (await placed.WaitAsync(Timeout, Ct)).OrderId);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldReturnResponsesOverAmazonSqs()
        {
            var serviceName = _serviceNames.Create("requests");
            using var server = await StartHostAsync(serviceName, service => service.AsServer()
                .HandleRequest<GetQuote, Quote>((request, context, ct) => Task.FromResult(new Quote { Price = 9.99m })));
            using var client = await StartHostAsync(serviceName, service => service.AsClient());

            var quote = await client.Services.GetRequiredService<IServiceBusClient>()
                .RequestAsync<GetQuote, Quote>(new GetQuote { Sku = "sku-1" }, Timeout, Ct);

            Assert.Equal(9.99m, quote.Price);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldReturnEachResponseToTheBusThatAsked()
        {
            var serviceName = _serviceNames.Create("reply-to");
            using var server = await StartHostAsync(serviceName, service => service.AsServer()
                .HandleRequest<GetQuote, Quote>((request, context, ct) => Task.FromResult(new Quote { Price = request.Sku == "sku-a" ? 1m : 2m })));
            using var clientA = await StartHostAsync(serviceName, service => service.AsClient());
            using var clientB = await StartHostAsync(serviceName, service => service.AsClient());

            var quoteA = clientA.Services.GetRequiredService<IServiceBusClient>().RequestAsync<GetQuote, Quote>(new GetQuote { Sku = "sku-a" }, Timeout, Ct);
            var quoteB = clientB.Services.GetRequiredService<IServiceBusClient>().RequestAsync<GetQuote, Quote>(new GetQuote { Sku = "sku-b" }, Timeout, Ct);

            Assert.Equal(1m, (await quoteA).Price);
            Assert.Equal(2m, (await quoteB).Price);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldReturnTheResponseToEachOfABusesConcurrentRequests()
        {
            // Every request in flight listens on the bus's one reply queue
            var serviceName = _serviceNames.Create("concurrent-requests");
            using var server = await StartHostAsync(serviceName, service => service.AsServer()
                .HandleRequest<GetQuote, Quote>(async (request, context, ct) =>
                {
                    await Task.Delay(200, ct);
                    return new Quote { Price = decimal.Parse(request.Sku) };
                }, handler => handler.MaxConcurrency = 10), configureOptions: options => options.MaxConcurrentCalls = 10);
            using var client = await StartHostAsync(serviceName, service => service.AsClient());
            var bus = client.Services.GetRequiredService<IServiceBusClient>();

            var quotes = await Task.WhenAll(Enumerable.Range(1, 10).Select(i => bus.RequestAsync<GetQuote, Quote>(new GetQuote { Sku = $"{i}" }, Timeout, Ct)));

            Assert.Equal(Enumerable.Range(1, 10).Select(i => (decimal)i), quotes.Select(quote => quote.Price));
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldDeliverEachCommandToEverySubscriberOfTheBus()
        {
            var serviceName = _serviceNames.Create("subscribers");
            using var host = await StartHostAsync(serviceName);
            var bus = host.Services.GetRequiredService<IServiceBus>();
            var first = new ConcurrentQueue<string>();
            var second = new ConcurrentQueue<string>();
            using var firstSubscription = bus.Commands.OfType<PlaceOrder>().Subscribe(command => first.Enqueue(command.OrderId));
            using var secondSubscription = bus.Commands.OfType<PlaceOrder>().Subscribe(command => second.Enqueue(command.OrderId));

            for (var i = 0; i < 5; i++)
            {
                await bus.SendAsync(new PlaceOrder { OrderId = $"o-{i}" });
            }

            await WaitForAsync(() => first.Count >= 5 && second.Count >= 5 ? first : null);
            Assert.Equal(first.OrderBy(id => id), second.OrderBy(id => id));
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldHandleEachCommandOnceAcrossTheServicesBuses()
        {
            // Unlike the v6 transports, a service's buses share its command queue, so each command is handled once
            var serviceName = _serviceNames.Create("shared-commands");
            var handled = new ConcurrentQueue<string>();
            using var serverA = await StartHostAsync(serviceName, service => service.AsServer()
                .HandleCommand<PlaceOrder>((command, context, ct) => { handled.Enqueue(command.OrderId); return Task.CompletedTask; }));
            using var serverB = await StartHostAsync(serviceName, service => service.AsServer()
                .HandleCommand<PlaceOrder>((command, context, ct) => { handled.Enqueue(command.OrderId); return Task.CompletedTask; }));
            using var client = await StartHostAsync(serviceName, service => service.AsClient());
            var bus = client.Services.GetRequiredService<IServiceBusClient>();

            for (var i = 0; i < 10; i++)
            {
                await bus.SendAsync(new PlaceOrder { OrderId = $"o-{i}" });
            }

            await WaitForAsync(() => handled.Count >= 10 ? handled : null);
            await Task.Delay(TimeSpan.FromSeconds(2), Ct);
            Assert.Equal(Enumerable.Range(0, 10).Select(i => $"o-{i}").OrderBy(id => id), handled.OrderBy(id => id));
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldDeliverEventsToEveryBus()
        {
            var serviceName = _serviceNames.Create("every-bus");
            using var server = await StartHostAsync(serviceName, service => service.AsServer());
            using var clientA = await StartHostAsync(serviceName, service => service.AsClient());
            using var clientB = await StartHostAsync(serviceName, service => service.AsClient());

            var receivedByA = clientA.Services.GetRequiredService<IServiceBusClient>().Events.OfType<OrderPlaced>().FirstAsync().ToTask();
            var receivedByB = clientB.Services.GetRequiredService<IServiceBusClient>().Events.OfType<OrderPlaced>().FirstAsync().ToTask();
            await server.Services.GetRequiredService<IServiceBus>().PublishAsync(new OrderPlaced { OrderId = "o-1" });

            Assert.Equal("o-1", (await receivedByA.WaitAsync(Timeout, Ct)).OrderId);
            Assert.Equal("o-1", (await receivedByB.WaitAsync(Timeout, Ct)).OrderId);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldKeepWorkingWhenAnotherHostInTheProcessStops()
        {
            var serviceName = _serviceNames.Create("two-hosts");
            using var hostA = await StartHostAsync(serviceName);
            var hostB = await StartHostAsync(serviceName);
            await hostB.StopAsync(Ct);
            hostB.Dispose();
            var busA = hostA.Services.GetRequiredService<IServiceBus>();

            var command = busA.Commands.OfType<PlaceOrder>().FirstAsync().ToTask();
            var placed = busA.Events.OfType<OrderPlaced>().FirstAsync().ToTask();
            await busA.SendAsync(new PlaceOrder { OrderId = "o-1" });
            await busA.PublishAsync(new OrderPlaced { OrderId = "o-1" });

            Assert.Equal("o-1", (await command.WaitAsync(Timeout, Ct)).OrderId);
            Assert.Equal("o-1", (await placed.WaitAsync(Timeout, Ct)).OrderId);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldNotDeadlockWhenASubscriberDisposesTheBus()
        {
            var host = await StartHostAsync(_serviceNames.Create("dispose-in-callback"));
            var bus = host.Services.GetRequiredService<IServiceBus>();
            var disposed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var subscription = bus.Commands.OfType<PlaceOrder>().Subscribe(_ =>
            {
                host.Dispose();
                disposed.SetResult(true);
            });

            await bus.SendAsync(new PlaceOrder { OrderId = "o-1" });

            Assert.True(await disposed.Task.WaitAsync(Timeout, Ct));
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldStopPromptlyWhileWaitingForMessages()
        {
            // The receive loop long-polls for up to 20 seconds; stopping mustn't wait for the poll to end
            var host = await StartHostAsync(_serviceNames.Create("stop-promptly"));
            var stopwatch = Stopwatch.StartNew();

            await host.StopAsync(Ct);
            host.Dispose();

            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10), $"Stopping took {stopwatch.Elapsed}.");
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldReceiveAgainOnceAQueueDeletedFromUnderTheBusIsBack()
        {
            // The bus reports the missing queue and subscribes again, which mustn't replay the error it just reported
            var serviceName = _serviceNames.Create("queue-deleted");
            using var host = await StartHostAsync(serviceName, configureOptions: options => options.CleanUpAbandonedQueues = false);
            var bus = host.Services.GetRequiredService<IServiceBus>();
            var command = bus.Commands.OfType<PlaceOrder>().FirstAsync().ToTask();
            using var sqs = TestBroker.CreateSqsClient();
            var commandsAttributes = await TestBroker.QueueAttributesAsync($"{serviceName}-commands", Ct);

            await sqs.DeleteQueueAsync(await TestBroker.QueueUrlAsync($"{serviceName}-commands", Ct), Ct);
            await Task.Delay(TimeSpan.FromSeconds(3), Ct);
            await sqs.CreateQueueAsync(new CreateQueueRequest
            {
                QueueName = $"{serviceName}-commands",
                Attributes = new Dictionary<string, string> { ["RedrivePolicy"] = commandsAttributes["RedrivePolicy"] },
            }, Ct);
            await bus.SendAsync(new PlaceOrder { OrderId = "o-1" });

            Assert.Equal("o-1", (await command.WaitAsync(Timeout, Ct)).OrderId);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldContinueTheSendersTraceAcrossAmazonSqs()
        {
            var serviceName = _serviceNames.Create("tracing");
            using var activities = new ActivityRecorder();
            using var receiver = await StartHostAsync(serviceName, service => service.AsServer()
                .HandleCommand<PlaceOrder>((command, context, ct) => Task.CompletedTask));
            using var sender = await StartHostAsync(serviceName, service => service.AsClient());

            await sender.Services.GetRequiredService<IServiceBusClient>().SendAsync(new PlaceOrder { OrderId = "o-1" });

            var send = await WaitForAsync(() => activities.For(serviceName).SingleOrDefault(activity => activity.DisplayName == $"send {serviceName}"));
            var process = await WaitForAsync(() => activities.For(serviceName).SingleOrDefault(activity => activity.DisplayName == $"process {serviceName}"));
            Assert.Equal(send.TraceId, process.TraceId);
            Assert.Equal(send.SpanId, process.ParentSpanId);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldContinueThePublishersTraceAcrossSns()
        {
            var serviceName = _serviceNames.Create("event-tracing");
            using var activities = new ActivityRecorder();
            using var receiver = await StartHostAsync(serviceName, service => service.AsClient()
                .HandleEvent<OrderPlaced>((ev, context, ct) => Task.CompletedTask));
            using var publisher = await StartHostAsync(serviceName, service => service.AsServer());

            await publisher.Services.GetRequiredService<IServiceBus>().PublishAsync(new OrderPlaced { OrderId = "o-1" });

            var publish = await WaitForAsync(() => activities.For(serviceName).SingleOrDefault(activity => activity.DisplayName == $"publish {serviceName}"));
            var process = await WaitForAsync(() => activities.For(serviceName).SingleOrDefault(activity => activity.DisplayName == $"process {serviceName}"));
            Assert.Equal(publish.TraceId, process.TraceId);
            Assert.Equal(publish.SpanId, process.ParentSpanId);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldCarryHeadersTheAppSetsOverAmazonSqs()
        {
            // More headers than SQS allows message attributes
            var serviceName = _serviceNames.Create("headers");
            var received = new TaskCompletionSource<IReadOnlyDictionary<string, string>>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var receiver = await StartHostAsync(serviceName, service => service.AsServer()
                .HandleCommand<PlaceOrder>((command, context, ct) =>
                {
                    received.TrySetResult(ObvsMessageHeaders.Get(command));
                    return Task.CompletedTask;
                }));
            using var sender = await StartHostAsync(serviceName, service => service.AsClient());
            var placeOrder = new PlaceOrder { OrderId = "o-1" };
            var headers = Enumerable.Range(0, 15).ToDictionary(i => $"x-header-{i}", i => $"value-{i}");
            headers["x-tenant"] = "acme";
            ObvsMessageHeaders.Set(placeOrder, headers);

            await sender.Services.GetRequiredService<IServiceBusClient>().SendAsync(placeOrder);

            var arrived = await received.Task.WaitAsync(Timeout, Ct);
            Assert.Equal("acme", arrived["x-tenant"]);
            Assert.Equal("value-14", arrived["x-header-14"]);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldCarryHeadersOnEventsOverSns()
        {
            var serviceName = _serviceNames.Create("event-headers");
            using var host = await StartHostAsync(serviceName);
            var bus = host.Services.GetRequiredService<IServiceBus>();
            var placed = new OrderPlaced { OrderId = "o-1" };
            ObvsMessageHeaders.Set(placed, new Dictionary<string, string> { ["x-tenant"] = "acme" });

            var received = bus.Events.OfType<OrderPlaced>().FirstAsync().ToTask();
            await bus.PublishAsync(placed);

            Assert.Equal("acme", ObvsMessageHeaders.Get(await received.WaitAsync(Timeout, Ct))["x-tenant"]);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldFailToSendAMessageOverTheSizeLimit()
        {
            using var host = await StartHostAsync(_serviceNames.Create("too-large"));
            var bus = host.Services.GetRequiredService<IServiceBus>();

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => bus.SendAsync(new PlaceOrder { OrderId = new string('o', 300 * 1024) }));

            Assert.Contains("256 KiB", exception.Message);
        }

        internal static async Task<IHost> StartHostAsync(string serviceName, Action<ObvsServiceBuilder> configure = null, string instanceId = null,
            Action<AmazonSqsOptions> configureOptions = null)
        {
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
            builder.Services.AddObvs(obvs =>
            {
                if (instanceId != null)
                {
                    obvs.UseInstanceId(instanceId);
                }
                obvs.AddService<IOrderMessage>(serviceName, service =>
                {
                    service
                        .UseAmazonSqs(options =>
                        {
                            TestBroker.Configure(options);
                            configureOptions?.Invoke(options);
                        })
                        .SerializedWith(new JsonMessageSerializer(), new JsonMessageDeserializerFactory(typeof(JsonMessageDeserializer<>)));
                    configure?.Invoke(service);
                });
            });
            var host = builder.Build();
            await host.StartAsync(Ct);
            return host;
        }

        internal static async Task<string> TopicArnAsync(string topicName)
        {
            using var sns = TestBroker.CreateSnsClient();
            string nextToken = null;
            do
            {
                var page = await sns.ListTopicsAsync(new ListTopicsRequest { NextToken = nextToken }, Ct);
                var topic = page.Topics?.FirstOrDefault(t => t.TopicArn.EndsWith(":" + topicName, StringComparison.Ordinal));
                if (topic != null)
                {
                    return topic.TopicArn;
                }
                nextToken = page.NextToken;
            }
            while (nextToken != null);
            return null;
        }

        internal static async Task<List<Subscription>> SubscriptionsAsync(string topicArn)
        {
            using var sns = TestBroker.CreateSnsClient();
            return (await sns.ListSubscriptionsByTopicAsync(topicArn, Ct)).Subscriptions ?? new List<Subscription>();
        }

        internal static async Task<T> WaitForAsync<T>(Func<T> find) where T : class
        {
            var deadline = DateTime.UtcNow + Timeout;
            T found;
            while ((found = find()) == null && DateTime.UtcNow < deadline)
            {
                await Task.Delay(20, Ct);
            }
            return found ?? throw new TimeoutException($"The expected {typeof(T).Name} wasn't recorded in time.");
        }

        // Names services uniquely for this run, and deletes their queues and topics afterwards
        public sealed class ServiceNames : IAsyncDisposable
        {
            private readonly string _runId = Guid.NewGuid().ToString("N").Substring(0, 8);

            public string Create(string name) => $"obvs-sqs-{name}-{_runId}";

            public async ValueTask DisposeAsync()
            {
                using var sqs = TestBroker.CreateSqsClient();
                var queues = await sqs.ListQueuesAsync(new ListQueuesRequest { QueueNamePrefix = "obvs-sqs-" });
                foreach (var queueUrl in queues.QueueUrls ?? new List<string>())
                {
                    if (queueUrl.Contains(_runId))
                    {
                        await sqs.DeleteQueueAsync(queueUrl);
                    }
                }

                using var sns = TestBroker.CreateSnsClient();
                string nextToken = null;
                do
                {
                    var page = await sns.ListTopicsAsync(new ListTopicsRequest { NextToken = nextToken });
                    foreach (var topic in page.Topics ?? new List<Topic>())
                    {
                        if (topic.TopicArn.Contains(_runId))
                        {
                            foreach (var subscription in (await sns.ListSubscriptionsByTopicAsync(topic.TopicArn)).Subscriptions ?? new List<Subscription>())
                            {
                                await sns.UnsubscribeAsync(subscription.SubscriptionArn);
                            }
                            await sns.DeleteTopicAsync(topic.TopicArn);
                        }
                    }
                    nextToken = page.NextToken;
                }
                while (nextToken != null);
            }
        }

        private sealed class ActivityRecorder : IDisposable
        {
            private readonly ConcurrentQueue<Activity> _stopped = new ConcurrentQueue<Activity>();
            private readonly ActivityListener _listener;

            public ActivityRecorder()
            {
                _listener = new ActivityListener
                {
                    ShouldListenTo = source => source.Name == ObvsTelemetry.ActivitySourceName,
                    Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                    ActivityStopped = activity => _stopped.Enqueue(activity),
                };
                ActivitySource.AddActivityListener(_listener);
            }

            public IReadOnlyList<Activity> For(string serviceName)
            {
                return _stopped.Where(activity => (activity.GetTagItem("messaging.destination.name") as string) == serviceName).ToList();
            }

            public void Dispose() => _listener.Dispose();
        }
    }
}
