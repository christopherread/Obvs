using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Obvs.Hosting;
using Obvs.Serialization.Json;
using Xunit;

namespace Obvs.AzureSB.Tests.Hosting
{
    // End to end through AddObvs and UseAzureServiceBus against the Service Bus emulator (see Emulator/docker-compose.yml).
    // Service names are unique to each run, so every run creates its entities, and the fixture deletes them afterwards.
    public class TestAzureServiceBusHosting : IClassFixture<TestAzureServiceBusHosting.ServiceNames>, IAsyncDisposable
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);
        private readonly ServiceNames _serviceNames;

        public TestAzureServiceBusHosting(ServiceNames serviceNames)
        {
            _serviceNames = serviceNames;
        }

        public ValueTask DisposeAsync() => _serviceNames.DeleteCreatedAsync();

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldCreateTheServicesEntitiesAndTheBusesOwn()
        {
            var serviceName = _serviceNames.Create("entities");
            const string instanceId = "orders-worker-1";

            using var host = await StartHostAsync(serviceName, instanceId: instanceId);

            var admin = TestBroker.CreateAdministrationClient();
            var ct = TestContext.Current.CancellationToken;
            Assert.True((await admin.QueueExistsAsync($"{serviceName}.Commands", ct)).Value);
            Assert.True((await admin.QueueExistsAsync($"{serviceName}.Requests", ct)).Value);
            Assert.True((await admin.TopicExistsAsync($"{serviceName}.Events", ct)).Value);
            Assert.True((await admin.SubscriptionExistsAsync($"{serviceName}.Events", instanceId, ct)).Value);
            Assert.True((await admin.QueueExistsAsync($"{serviceName}.Responses.{instanceId}", ct)).Value);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldDeleteTheBusesOwnEntitiesWhenItStops()
        {
            var serviceName = _serviceNames.Create("cleanup");
            const string instanceId = "orders-worker-1";
            var host = await StartHostAsync(serviceName, instanceId: instanceId);

            await host.StopAsync(TestContext.Current.CancellationToken);
            host.Dispose();

            var admin = TestBroker.CreateAdministrationClient();
            var ct = TestContext.Current.CancellationToken;
            Assert.False((await admin.SubscriptionExistsAsync($"{serviceName}.Events", instanceId, ct)).Value);
            Assert.False((await admin.QueueExistsAsync($"{serviceName}.Responses.{instanceId}", ct)).Value);
            Assert.True((await admin.QueueExistsAsync($"{serviceName}.Commands", ct)).Value);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldSendCommandsAndPublishEventsOverAzureServiceBus()
        {
            using var host = await StartHostAsync(_serviceNames.Create("commands-events"));
            var bus = host.Services.GetRequiredService<IServiceBus>();

            var command = bus.Commands.OfType<PlaceOrder>().FirstAsync().ToTask();
            await bus.SendAsync(new PlaceOrder { OrderId = "o-1" }, TestContext.Current.CancellationToken);
            Assert.Equal("o-1", (await command.WaitAsync(Timeout, TestContext.Current.CancellationToken)).OrderId);

            var placed = bus.Events.OfType<OrderPlaced>().FirstAsync().ToTask();
            await bus.PublishAsync(new OrderPlaced { OrderId = "o-1" }, TestContext.Current.CancellationToken);
            Assert.Equal("o-1", (await placed.WaitAsync(Timeout, TestContext.Current.CancellationToken)).OrderId);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldReturnResponsesOverAzureServiceBus()
        {
            var serviceName = _serviceNames.Create("requests");
            using var server = await StartHostAsync(serviceName, service => service.AsServer()
                .HandleRequest<GetQuote, Quote>((request, context, ct) => Task.FromResult(new Quote { Price = 9.99m })));
            using var client = await StartHostAsync(serviceName, service => service.AsClient());

            var quote = await client.Services.GetRequiredService<IServiceBusClient>()
                .RequestAsync<GetQuote, Quote>(new GetQuote { Sku = "sku-1" }, Timeout, TestContext.Current.CancellationToken);

            Assert.Equal(9.99m, quote.Price);
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
                await bus.SendAsync(new PlaceOrder { OrderId = $"o-{i}" }, TestContext.Current.CancellationToken);
            }

            await WaitForAsync(() => handled.Count >= 10 ? handled : null);
            await Task.Delay(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
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
            await server.Services.GetRequiredService<IServiceBus>().PublishAsync(new OrderPlaced { OrderId = "o-1" }, TestContext.Current.CancellationToken);

            Assert.Equal("o-1", (await receivedByA.WaitAsync(Timeout, TestContext.Current.CancellationToken)).OrderId);
            Assert.Equal("o-1", (await receivedByB.WaitAsync(Timeout, TestContext.Current.CancellationToken)).OrderId);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldKeepWorkingWhenAnotherHostInTheProcessStops()
        {
            var serviceName = _serviceNames.Create("two-hosts");
            using var hostA = await StartHostAsync(serviceName);
            var hostB = await StartHostAsync(serviceName);
            await hostB.StopAsync(TestContext.Current.CancellationToken);
            hostB.Dispose();
            var busA = hostA.Services.GetRequiredService<IServiceBus>();

            var command = busA.Commands.OfType<PlaceOrder>().FirstAsync().ToTask();
            await busA.SendAsync(new PlaceOrder { OrderId = "o-1" }, TestContext.Current.CancellationToken);

            Assert.Equal("o-1", (await command.WaitAsync(Timeout, TestContext.Current.CancellationToken)).OrderId);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldReceiveAgainOnceAQueueDeletedFromUnderTheBusIsBack()
        {
            // The bus reports the missing queue and subscribes again, which mustn't replay the error it just reported
            var serviceName = _serviceNames.Create("queue-deleted");
            using var host = await StartHostAsync(serviceName);
            var bus = host.Services.GetRequiredService<IServiceBus>();
            var command = bus.Commands.OfType<PlaceOrder>().FirstAsync().ToTask();
            var admin = TestBroker.CreateAdministrationClient();

            await admin.DeleteQueueAsync($"{serviceName}.Commands", TestContext.Current.CancellationToken);
            await Task.Delay(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
            await admin.CreateQueueAsync($"{serviceName}.Commands", TestContext.Current.CancellationToken);
            await bus.SendAsync(new PlaceOrder { OrderId = "o-1" }, TestContext.Current.CancellationToken);

            Assert.Equal("o-1", (await command.WaitAsync(Timeout, TestContext.Current.CancellationToken)).OrderId);
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

            await bus.SendAsync(new PlaceOrder { OrderId = "o-1" }, TestContext.Current.CancellationToken);

            Assert.True(await disposed.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken));
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldContinueTheSendersTraceAcrossAzureServiceBus()
        {
            var serviceName = _serviceNames.Create("tracing");
            using var activities = new ActivityRecorder();
            using var receiver = await StartHostAsync(serviceName, service => service.AsServer()
                .HandleCommand<PlaceOrder>((command, context, ct) => Task.CompletedTask));
            using var sender = await StartHostAsync(serviceName, service => service.AsClient());

            await sender.Services.GetRequiredService<IServiceBusClient>().SendAsync(new PlaceOrder { OrderId = "o-1" }, TestContext.Current.CancellationToken);

            var send = await WaitForAsync(() => activities.For(serviceName).SingleOrDefault(activity => activity.DisplayName == $"send {serviceName}"));
            var process = await WaitForAsync(() => activities.For(serviceName).SingleOrDefault(activity => activity.DisplayName == $"process {serviceName}"));
            Assert.Equal(send.TraceId, process.TraceId);
            Assert.Equal(send.SpanId, process.ParentSpanId);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldCarryHeadersTheAppSetsOverAzureServiceBus()
        {
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
            ObvsMessageHeaders.Set(placeOrder, new Dictionary<string, string> { ["x-tenant"] = "acme" });

            await sender.Services.GetRequiredService<IServiceBusClient>().SendAsync(placeOrder, TestContext.Current.CancellationToken);

            var headers = await received.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
            Assert.Equal("acme", headers["x-tenant"]);
        }

        private static async Task<IHost> StartHostAsync(string serviceName, Action<ObvsServiceBuilder> configure = null, string instanceId = null)
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
                        .UseAzureServiceBus(options => options.ConnectionString = TestBroker.ConnectionString)
                        .SerializedWith(new JsonMessageSerializer(), new JsonMessageDeserializerFactory(typeof(JsonMessageDeserializer<>)));
                    configure?.Invoke(service);
                });
            });
            var host = builder.Build();
            await host.StartAsync(TestContext.Current.CancellationToken);
            return host;
        }

        private static async Task<T> WaitForAsync<T>(Func<T> find) where T : class
        {
            var deadline = DateTime.UtcNow + Timeout;
            T found;
            while ((found = find()) == null && DateTime.UtcNow < deadline)
            {
                await Task.Delay(20, TestContext.Current.CancellationToken);
            }
            return found ?? throw new TimeoutException($"The expected {typeof(T).Name} wasn't recorded in time.");
        }

        // Names services uniquely for this run, and deletes their queues and topics afterwards
        public sealed class ServiceNames : IAsyncDisposable
        {
            private readonly string _runId = Guid.NewGuid().ToString("N").Substring(0, 8);
            private readonly ConcurrentQueue<string> _created = new ConcurrentQueue<string>();

            public string Create(string name)
            {
                var serviceName = $"obvs-asb-{name}-{_runId}";
                _created.Enqueue(serviceName);
                return serviceName;
            }

            // The emulator allows 50 queues, and the test classes run at the same time, so each test's queues and
            // topics are deleted as it finishes rather than with the rest of its class's
            public async ValueTask DeleteCreatedAsync()
            {
                var prefixes = new List<string>();
                while (_created.TryDequeue(out var serviceName))
                {
                    prefixes.Add(serviceName + ".");
                }
                if (prefixes.Count == 0)
                {
                    return;
                }

                var admin = TestBroker.CreateAdministrationClient();
                await foreach (var queue in admin.GetQueuesAsync())
                {
                    if (prefixes.Any(prefix => queue.Name.StartsWith(prefix, StringComparison.Ordinal)))
                    {
                        await admin.DeleteQueueAsync(queue.Name);
                    }
                }
                await foreach (var topic in admin.GetTopicsAsync())
                {
                    if (prefixes.Any(prefix => topic.Name.StartsWith(prefix, StringComparison.Ordinal)))
                    {
                        await admin.DeleteTopicAsync(topic.Name);
                    }
                }
            }

            public async ValueTask DisposeAsync()
            {
                var admin = TestBroker.CreateAdministrationClient();
                await foreach (var queue in admin.GetQueuesAsync())
                {
                    if (queue.Name.Contains(_runId))
                    {
                        await admin.DeleteQueueAsync(queue.Name);
                    }
                }
                await foreach (var topic in admin.GetTopicsAsync())
                {
                    if (topic.Name.Contains(_runId))
                    {
                        await admin.DeleteTopicAsync(topic.Name);
                    }
                }
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
