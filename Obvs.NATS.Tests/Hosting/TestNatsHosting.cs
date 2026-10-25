using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Obvs.Hosting;
using Obvs.Serialization.Json;
using Xunit;

namespace Obvs.NATS.Tests.Hosting
{
    // End to end through AddObvs and UseNats against a real server (NATS_URL, default nats://localhost:4222).
    // Each test uses its own service name, so its subjects don't meet the other tests'.
    public class TestNatsHosting
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldSendCommandsAndPublishEventsOverNats()
        {
            using var host = await StartHostAsync("obvs-nats-commands-events");
            var bus = host.Services.GetRequiredService<IServiceBus>();

            var command = bus.Commands.OfType<PlaceOrder>().FirstAsync().ToTask();
            await bus.SendAsync(new PlaceOrder { OrderId = "o-1" });
            Assert.Equal("o-1", (await command.WaitAsync(Timeout, TestContext.Current.CancellationToken)).OrderId);

            var placed = bus.Events.OfType<OrderPlaced>().FirstAsync().ToTask();
            await bus.PublishAsync(new OrderPlaced { OrderId = "o-1" });
            Assert.Equal("o-1", (await placed.WaitAsync(Timeout, TestContext.Current.CancellationToken)).OrderId);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldReturnResponsesOverNats()
        {
            using var host = await StartHostAsync("obvs-nats-requests", service => service
                .HandleRequest<GetQuote, Quote>((request, context, ct) => Task.FromResult(new Quote { Price = 9.99m })));

            var quote = await host.Services.GetRequiredService<IServiceBusClient>()
                .RequestAsync<GetQuote, Quote>(new GetQuote { Sku = "sku-1" }, Timeout, TestContext.Current.CancellationToken);

            Assert.Equal(9.99m, quote.Price);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldKeepWorkingWhenAnotherHostInTheProcessStops()
        {
            // Two hosts in one process, as tests have: stopping one mustn't close the other's connection
            using var hostA = await StartHostAsync("obvs-nats-two-hosts");
            var hostB = await StartHostAsync("obvs-nats-two-hosts");
            await hostB.StopAsync(TestContext.Current.CancellationToken);
            hostB.Dispose();
            var busA = hostA.Services.GetRequiredService<IServiceBus>();

            var command = busA.Commands.OfType<PlaceOrder>().FirstAsync().ToTask();
            await busA.SendAsync(new PlaceOrder { OrderId = "o-1" });

            Assert.Equal("o-1", (await command.WaitAsync(Timeout, TestContext.Current.CancellationToken)).OrderId);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldNotDeadlockWhenASubscriberDisposesTheBus()
        {
            var host = await StartHostAsync("obvs-nats-dispose-in-callback");
            var bus = host.Services.GetRequiredService<IServiceBus>();
            var disposed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var subscription = bus.Commands.OfType<PlaceOrder>().Subscribe(_ =>
            {
                host.Dispose();
                disposed.SetResult(true);
            });

            await bus.SendAsync(new PlaceOrder { OrderId = "o-1" });

            Assert.True(await disposed.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken));
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldContinueTheSendersTraceAcrossNats()
        {
            const string serviceName = "obvs-nats-tracing";
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
        public async Task ShouldCarryHeadersTheAppSetsOverNats()
        {
            const string serviceName = "obvs-nats-headers";
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

            await sender.Services.GetRequiredService<IServiceBusClient>().SendAsync(placeOrder);

            var headers = await received.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
            Assert.Equal("acme", headers["x-tenant"]);
        }

        private static async Task<IHost> StartHostAsync(string serviceName, Action<ObvsServiceBuilder> configure = null)
        {
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
            builder.Services.AddObvs(obvs => obvs.AddService<IOrderMessage>(serviceName, service =>
            {
                service
                    .UseNats(options => options.Url = TestBroker.Url)
                    .SerializedWith(new JsonMessageSerializer(), new JsonMessageDeserializerFactory(typeof(JsonMessageDeserializer<>)));
                configure?.Invoke(service);
            }));
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
