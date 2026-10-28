using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Obvs.Hosting;
using Obvs.Serialization.Json;
using Xunit;

namespace Obvs.Kafka.Tests.Hosting
{
    // End to end through AddObvs and UseKafka against a real broker (KAFKA_BOOTSTRAP_SERVERS, default localhost:9092).
    // Service names are unique to each run, so every run starts without the service's topics and creates them.
    public class TestKafkaHosting : IClassFixture<TestKafkaHosting.ServiceNames>
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
        private readonly ServiceNames _serviceNames;

        public TestKafkaHosting(ServiceNames serviceNames)
        {
            _serviceNames = serviceNames;
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldCreateTheServicesTopics()
        {
            var serviceName = _serviceNames.Create("topics");

            using var host = await StartHostAsync(serviceName);

            using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = TestBroker.BootstrapServers }).Build();
            var topics = admin.GetMetadata(Timeout).Topics.Select(topic => topic.Topic).ToList();
            Assert.Contains($"{serviceName}.Commands", topics);
            Assert.Contains($"{serviceName}.Events", topics);
            Assert.Contains($"{serviceName}.Requests", topics);
            Assert.Contains($"{serviceName}.Responses", topics);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldSendCommandsAndPublishEventsOverKafka()
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
        public async Task ShouldReturnResponsesOverKafka()
        {
            using var host = await StartHostAsync(_serviceNames.Create("requests"), service => service
                .HandleRequest<GetQuote, Quote>((request, context, ct) => Task.FromResult(new Quote { Price = 9.99m })));

            var quote = await host.Services.GetRequiredService<IServiceBusClient>()
                .RequestAsync(new GetQuote { Sku = "sku-1" }, Timeout, TestContext.Current.CancellationToken);

            Assert.Equal(9.99m, quote.Price);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldHandleEachCommandOnceAcrossTheServicesBuses()
        {
            // The service's buses share a consumer group for commands, so Kafka gives each command to one of them
            var serviceName = _serviceNames.Create("command-once");
            var handled = new ConcurrentQueue<string>();
            Action<ObvsServiceBuilder> handle = service => service.AsServer()
                .HandleCommand<PlaceOrder>((command, context, ct) =>
                {
                    handled.Enqueue(command.OrderId);
                    return Task.CompletedTask;
                });
            using var hostA = await StartHostAsync(serviceName, handle);
            using var hostB = await StartHostAsync(serviceName, handle);
            using var sender = await StartHostAsync(serviceName, service => service.AsClient());
            var orderIds = Enumerable.Range(0, 10).Select(i => $"o-{i}").ToList();

            foreach (var orderId in orderIds)
            {
                await sender.Services.GetRequiredService<IServiceBusClient>().SendAsync(new PlaceOrder { OrderId = orderId }, TestContext.Current.CancellationToken);
            }

            await WaitForAsync(() => handled.Count >= orderIds.Count ? handled : null);
            // Long enough for a second delivery to arrive, if there were one
            await Task.Delay(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
            Assert.Equal(orderIds, handled.OrderBy(orderId => orderId));
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldDeliverEveryEventToEveryBus()
        {
            // As with the other transports, each bus receives every event, so they have consumer groups of their own
            var serviceName = _serviceNames.Create("every-bus");
            using var hostA = await StartHostAsync(serviceName);
            using var hostB = await StartHostAsync(serviceName);
            var busA = hostA.Services.GetRequiredService<IServiceBus>();
            var busB = hostB.Services.GetRequiredService<IServiceBus>();

            var receivedByA = busA.Events.OfType<OrderPlaced>().FirstAsync().ToTask();
            var receivedByB = busB.Events.OfType<OrderPlaced>().FirstAsync().ToTask();
            await busA.PublishAsync(new OrderPlaced { OrderId = "o-1" }, TestContext.Current.CancellationToken);

            Assert.Equal("o-1", (await receivedByA.WaitAsync(Timeout, TestContext.Current.CancellationToken)).OrderId);
            Assert.Equal("o-1", (await receivedByB.WaitAsync(Timeout, TestContext.Current.CancellationToken)).OrderId);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldHandleCommandsSentWhileTheServiceWasDown()
        {
            // The commands group outlives the bus, so a bus started later, even with another instance ID, carries on from it
            var serviceName = _serviceNames.Create("commands-while-down");
            var handled = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            Action<ObvsServiceBuilder> handle = service => service.AsServer()
                .HandleCommand<PlaceOrder>((command, context, ct) =>
                {
                    handled.TrySetResult(command.OrderId);
                    return Task.CompletedTask;
                });
            var first = await StartHostAsync(serviceName, handle);
            await first.StopAsync(TestContext.Current.CancellationToken);
            first.Dispose();
            using var sender = await StartHostAsync(serviceName, service => service.AsClient());

            await sender.Services.GetRequiredService<IServiceBusClient>().SendAsync(new PlaceOrder { OrderId = "o-1" }, TestContext.Current.CancellationToken);
            using var restarted = await StartHostAsync(serviceName, handle);

            Assert.Equal("o-1", await handled.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken));
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldReceiveEventsPublishedWhileABusWithAFixedInstanceIdWasDown()
        {
            // The bus's events group is named after its instance ID, so restarting with the same one carries on from it
            var serviceName = _serviceNames.Create("events-while-down");
            var instanceId = $"subscriber-{Guid.NewGuid():N}";
            var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            Action<ObvsServiceBuilder> handle = service => service.AsClient()
                .HandleEvent<OrderPlaced>((ev, context, ct) =>
                {
                    received.TrySetResult(ev.OrderId);
                    return Task.CompletedTask;
                });
            var first = await StartHostAsync(serviceName, handle, instanceId);
            await first.StopAsync(TestContext.Current.CancellationToken);
            first.Dispose();
            using var publisher = await StartHostAsync(serviceName, service => service.AsServer());

            await publisher.Services.GetRequiredService<IServiceBus>().PublishAsync(new OrderPlaced { OrderId = "o-1" }, TestContext.Current.CancellationToken);
            using var restarted = await StartHostAsync(serviceName, handle, instanceId);

            Assert.Equal("o-1", await received.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken));
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
        public async Task ShouldContinueTheSendersTraceAcrossKafka()
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
        public async Task ShouldCarryHeadersTheAppSetsOverKafka()
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
                        .UseKafka(options => options.BootstrapServers = TestBroker.BootstrapServers)
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

        // Names services uniquely for this run, and deletes their topics and consumer groups afterwards
        public sealed class ServiceNames : IAsyncDisposable
        {
            private readonly string _runId = Guid.NewGuid().ToString("N").Substring(0, 8);

            public string Create(string name) => $"obvs-kafka-{name}-{_runId}";

            public async ValueTask DisposeAsync()
            {
                using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = TestBroker.BootstrapServers }).Build();
                bool IsThisRuns(string name) => name.StartsWith("obvs-kafka-") && name.Contains(_runId);
                var topics = admin.GetMetadata(Timeout).Topics.Select(topic => topic.Topic).Where(IsThisRuns).ToList();
                if (topics.Count > 0)
                {
                    await admin.DeleteTopicsAsync(topics);
                }
                var groups = (await admin.ListConsumerGroupsAsync()).Valid.Select(group => group.GroupId).Where(IsThisRuns).ToList();
                try
                {
                    if (groups.Count > 0)
                    {
                        await admin.DeleteGroupsAsync(groups);
                    }
                }
                catch (DeleteGroupsException exception) when (exception.Results.All(result => result.Error.Code == ErrorCode.NoError || result.Error.Code == ErrorCode.NonEmptyGroup))
                {
                    // Groups of hosts that were disposed without being stopped still have members. Kafka removes them later.
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
