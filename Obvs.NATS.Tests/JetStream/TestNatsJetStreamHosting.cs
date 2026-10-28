using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NATS.Client.JetStream.Models;
using Obvs.Hosting;
using Obvs.NATS.Tests.Hosting;
using Xunit;
using static Obvs.NATS.Tests.JetStream.JetStreamServices;

namespace Obvs.NATS.Tests.JetStream
{
    // End to end through AddObvs and UseNatsJetStream against a server with JetStream enabled (NATS_JETSTREAM_URL,
    // default nats://localhost:4223). Service names are unique to each run, so every run creates its streams, and the
    // fixture deletes them afterwards.
    public class TestNatsJetStreamHosting : IClassFixture<JetStreamServices>
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);
        private readonly JetStreamServices _services;

        public TestNatsJetStreamHosting(JetStreamServices services)
        {
            _services = services;
        }

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldCreateTheServicesStreamsAndConsumersAndTheBusesOwn()
        {
            var serviceName = _services.Create("streams");
            const string instanceId = "orders-worker-1";

            using var host = await StartHostAsync(serviceName, instanceId: instanceId);

            Assert.True(await _services.StreamExistsAsync($"{serviceName}_Commands"));
            Assert.True(await _services.StreamExistsAsync($"{serviceName}_Requests"));
            Assert.True(await _services.StreamExistsAsync($"{serviceName}_Events"));
            Assert.True(await _services.StreamExistsAsync($"{serviceName}_Responses"));
            Assert.True(await _services.StreamExistsAsync($"{serviceName}_DeadLetters"));

            // Commands and requests: one consumer the service's buses share
            var commands = await _services.ConsumerConfigAsync($"{serviceName}_Commands", serviceName);
            Assert.NotNull(commands);
            Assert.Equal(TimeSpan.Zero, commands.InactiveThreshold); // Never deleted for being idle
            Assert.NotNull(await _services.ConsumerConfigAsync($"{serviceName}_Requests", serviceName));

            // Events and responses: this bus's own, which delete themselves when idle so a crashed bus doesn't leave them behind
            var events = await _services.ConsumerConfigAsync($"{serviceName}_Events", instanceId);
            Assert.NotNull(events);
            Assert.Equal(TimeSpan.FromMinutes(5), events.InactiveThreshold);
            var responses = await _services.ConsumerConfigAsync($"{serviceName}_Responses", instanceId);
            Assert.NotNull(responses);
            Assert.Equal($"{serviceName}.Responses.{instanceId}.*", responses.FilterSubject);
            Assert.Equal(TimeSpan.FromMinutes(5), responses.InactiveThreshold);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldKeepCommandsUntilTakenAndEventsOnlyWhileABusListens()
        {
            var serviceName = _services.Create("retention");

            using var host = await StartHostAsync(serviceName);

            var commands = await _services.JetStream.GetStreamAsync($"{serviceName}_Commands", cancellationToken: Ct);
            Assert.Equal(StreamConfigRetention.Workqueue, commands.Info.Config.Retention);
            var events = await _services.JetStream.GetStreamAsync($"{serviceName}_Events", cancellationToken: Ct);
            Assert.Equal(StreamConfigRetention.Interest, events.Info.Config.Retention);
            var deadLetters = await _services.JetStream.GetStreamAsync($"{serviceName}_DeadLetters", cancellationToken: Ct);
            Assert.Equal(StreamConfigRetention.Limits, deadLetters.Info.Config.Retention);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldFailToStartWhenTheServerDoesntHaveJetStream()
        {
            // NATS_URL is a core NATS server, without JetStream
            var exception = await Assert.ThrowsAsync<ObvsConfigurationException>(() =>
                StartHostAsync(_services.Create("no-jetstream"), options: options => options.Url = TestBroker.Url));

            var error = Assert.Single(exception.Errors);
            Assert.Contains("JetStream enabled", error);
            Assert.Contains(TestBroker.Url, error);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldDeleteTheBusesOwnConsumersWhenItStops()
        {
            var serviceName = _services.Create("cleanup");
            const string instanceId = "orders-worker-1";
            var host = await StartHostAsync(serviceName, instanceId: instanceId);

            await host.StopAsync(Ct);
            host.Dispose();

            Assert.Null(await _services.ConsumerConfigAsync($"{serviceName}_Events", instanceId));
            Assert.Null(await _services.ConsumerConfigAsync($"{serviceName}_Responses", instanceId));
            Assert.NotNull(await _services.ConsumerConfigAsync($"{serviceName}_Commands", serviceName));
            Assert.True(await _services.StreamExistsAsync($"{serviceName}_Commands"));
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldNotReportAnErrorToSubscribersWhenTheBusStops()
        {
            // Deleting this bus's consumers as it stops mustn't fail receive loops still subscribed
            var host = await StartHostAsync(_services.Create("stop-quietly"));
            var bus = host.Services.GetRequiredService<IServiceBus>();
            var errors = new ConcurrentQueue<Exception>();
            var eventSubscription = bus.Events.Subscribe(_ => { }, errors.Enqueue);
            var commandSubscription = bus.Commands.Subscribe(_ => { }, errors.Enqueue);
            await Task.Delay(500, Ct);

            await host.StopAsync(Ct);
            host.Dispose();
            await Task.Delay(TimeSpan.FromSeconds(1), Ct);

            Assert.Empty(errors);
            eventSubscription.Dispose();
            commandSubscription.Dispose();
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldStartWhenTheStreamsAndConsumersAlreadyExist()
        {
            var serviceName = _services.Create("restart");
            const string instanceId = "orders-worker-1";
            using (var first = await StartHostAsync(serviceName, instanceId: instanceId))
            {
            }

            using var second = await StartHostAsync(serviceName, instanceId: instanceId);
            var bus = second.Services.GetRequiredService<IServiceBus>();
            var command = bus.Commands.OfType<PlaceOrder>().FirstAsync().ToTask();
            await bus.SendAsync(new PlaceOrder { OrderId = "o-1" }, TestContext.Current.CancellationToken);

            Assert.Equal("o-1", (await command.WaitAsync(Timeout, Ct)).OrderId);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldSendCommandsAndPublishEventsOverJetStream()
        {
            using var host = await StartHostAsync(_services.Create("commands-events"));
            var bus = host.Services.GetRequiredService<IServiceBus>();

            var command = bus.Commands.OfType<PlaceOrder>().FirstAsync().ToTask();
            await bus.SendAsync(new PlaceOrder { OrderId = "o-1" }, TestContext.Current.CancellationToken);
            Assert.Equal("o-1", (await command.WaitAsync(Timeout, Ct)).OrderId);

            var placed = bus.Events.OfType<OrderPlaced>().FirstAsync().ToTask();
            await bus.PublishAsync(new OrderPlaced { OrderId = "o-1" }, TestContext.Current.CancellationToken);
            Assert.Equal("o-1", (await placed.WaitAsync(Timeout, Ct)).OrderId);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldKeepACommandSentBeforeAnyServerStarts()
        {
            // Unlike core NATS, a command waits on its stream for the service to start
            var serviceName = _services.Create("durable");
            var handled = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var client = await StartHostAsync(serviceName, service => service.AsClient());
            await client.Services.GetRequiredService<IServiceBusClient>().SendAsync(new PlaceOrder { OrderId = "o-1" }, TestContext.Current.CancellationToken);

            using var server = await StartHostAsync(serviceName, service => service.AsServer()
                .HandleCommand<PlaceOrder>((command, context, ct) => { handled.TrySetResult(command.OrderId); return Task.CompletedTask; }));

            Assert.Equal("o-1", await handled.Task.WaitAsync(Timeout, Ct));
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldReturnResponsesOverJetStream()
        {
            var serviceName = _services.Create("requests");
            using var server = await StartHostAsync(serviceName, service => service.AsServer()
                .HandleRequest<GetQuote, Quote>((request, context, ct) => Task.FromResult(new Quote { Price = 9.99m })));
            using var client = await StartHostAsync(serviceName, service => service.AsClient());

            var quote = await client.Services.GetRequiredService<IServiceBusClient>()
                .RequestAsync(new GetQuote { Sku = "sku-1" }, Timeout, Ct);

            Assert.Equal(9.99m, quote.Price);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldReturnEachOfABusesConcurrentRequestsItsOwnResponse()
        {
            // Every pending request on a bus reads the same response consumer, so none can take another's response
            var serviceName = _services.Create("concurrent-requests");
            using var server = await StartHostAsync(serviceName, service => service.AsServer()
                .HandleRequest<GetQuote, Quote>(async (request, context, ct) =>
                {
                    await Task.Delay(200, ct);
                    return new Quote { Price = decimal.Parse(request.Sku) };
                }), options: options => options.MaxConcurrentMessages = 5);
            using var client = await StartHostAsync(serviceName, service => service.AsClient());
            var bus = client.Services.GetRequiredService<IServiceBusClient>();

            var quotes = await Task.WhenAll(Enumerable.Range(1, 5).Select(i =>
                bus.RequestAsync(new GetQuote { Sku = i.ToString() }, Timeout, Ct)));

            Assert.Equal(new[] { 1m, 2m, 3m, 4m, 5m }, quotes.Select(quote => quote.Price));
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldSendResponsesOnlyToTheRequestingBus()
        {
            var serviceName = _services.Create("reply-to");
            using var server = await StartHostAsync(serviceName, service => service.AsServer()
                .HandleRequest<GetQuote, Quote>((request, context, ct) => Task.FromResult(new Quote { Price = 9.99m })));
            using var clientA = await StartHostAsync(serviceName, service => service.AsClient(), instanceId: "client-a");
            using var clientB = await StartHostAsync(serviceName, service => service.AsClient(), instanceId: "client-b");
            // Watches every response on the service's stream
            var watcher = await _services.JetStream.CreateOrUpdateConsumerAsync($"{serviceName}_Responses",
                new ConsumerConfig("watcher") { FilterSubject = $"{serviceName}.Responses.>", AckPolicy = ConsumerConfigAckPolicy.None }, Ct);

            await clientA.Services.GetRequiredService<IServiceBusClient>().RequestAsync(new GetQuote { Sku = "sku-1" }, Timeout, Ct);

            var response = await watcher.NextAsync<byte[]>(cancellationToken: Ct);
            Assert.NotNull(response);
            Assert.Equal($"{serviceName}.Responses.client-a.Quote", response.Subject);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldHandleEachCommandOnceAcrossTheServicesBuses()
        {
            var serviceName = _services.Create("shared-commands");
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
            await Task.Delay(TimeSpan.FromSeconds(2), Ct);
            Assert.Equal(Enumerable.Range(0, 10).Select(i => $"o-{i}").OrderBy(id => id), handled.OrderBy(id => id));
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldDeliverEventsToEveryBus()
        {
            var serviceName = _services.Create("every-bus");
            using var server = await StartHostAsync(serviceName, service => service.AsServer());
            using var clientA = await StartHostAsync(serviceName, service => service.AsClient());
            using var clientB = await StartHostAsync(serviceName, service => service.AsClient());

            var receivedByA = clientA.Services.GetRequiredService<IServiceBusClient>().Events.OfType<OrderPlaced>().FirstAsync().ToTask();
            var receivedByB = clientB.Services.GetRequiredService<IServiceBusClient>().Events.OfType<OrderPlaced>().FirstAsync().ToTask();
            await server.Services.GetRequiredService<IServiceBus>().PublishAsync(new OrderPlaced { OrderId = "o-1" }, TestContext.Current.CancellationToken);

            Assert.Equal("o-1", (await receivedByA.WaitAsync(Timeout, Ct)).OrderId);
            Assert.Equal("o-1", (await receivedByB.WaitAsync(Timeout, Ct)).OrderId);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldKeepWorkingWhenAnotherHostInTheProcessStops()
        {
            var serviceName = _services.Create("two-hosts");
            using var hostA = await StartHostAsync(serviceName);
            var hostB = await StartHostAsync(serviceName);
            await hostB.StopAsync(Ct);
            hostB.Dispose();
            var busA = hostA.Services.GetRequiredService<IServiceBus>();

            var command = busA.Commands.OfType<PlaceOrder>().FirstAsync().ToTask();
            await busA.SendAsync(new PlaceOrder { OrderId = "o-1" }, TestContext.Current.CancellationToken);

            Assert.Equal("o-1", (await command.WaitAsync(Timeout, Ct)).OrderId);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldNotDeadlockWhenASubscriberDisposesTheBus()
        {
            var host = await StartHostAsync(_services.Create("dispose-in-callback"));
            var bus = host.Services.GetRequiredService<IServiceBus>();
            var disposed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var subscription = bus.Commands.OfType<PlaceOrder>().Subscribe(_ =>
            {
                host.Dispose();
                disposed.SetResult(true);
            });

            await bus.SendAsync(new PlaceOrder { OrderId = "o-1" }, TestContext.Current.CancellationToken);

            Assert.True(await disposed.Task.WaitAsync(Timeout, Ct));
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldContinueTheSendersTraceAcrossJetStream()
        {
            var serviceName = _services.Create("tracing");
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
        public async Task ShouldCarryHeadersTheAppSetsOverJetStream()
        {
            var serviceName = _services.Create("headers");
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

            var headers = await received.Task.WaitAsync(Timeout, Ct);
            Assert.Equal("acme", headers["x-tenant"]);
        }

        private static async Task<T> WaitForAsync<T>(Func<T> find) where T : class
        {
            var deadline = DateTime.UtcNow + Timeout;
            T found;
            while ((found = find()) == null && DateTime.UtcNow < deadline)
            {
                await Task.Delay(20, Ct);
            }
            return found ?? throw new TimeoutException($"The expected {typeof(T).Name} wasn't recorded in time.");
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
