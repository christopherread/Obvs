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

namespace Obvs.ActiveMQ.Tests.Hosting
{
    // End to end through AddObvs and UseActiveMq against a real broker (ACTIVEMQ_URI, default activemq:tcp://localhost:61616).
    // Each test uses its own service name, so its topics don't meet the other tests'.
    public class TestActiveMqHosting
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldSendCommandsAndPublishEventsOverActiveMq()
        {
            using var host = await StartHostAsync("obvs-activemq-commands-events");
            var bus = host.Services.GetRequiredService<IServiceBus>();

            var command = bus.Commands.OfType<PlaceOrder>().FirstAsync().ToTask();
            await bus.SendAsync(new PlaceOrder { OrderId = "o-1" }, TestContext.Current.CancellationToken);
            Assert.Equal("o-1", (await command.WaitAsync(Timeout, TestContext.Current.CancellationToken)).OrderId);

            var placed = bus.Events.OfType<OrderPlaced>().FirstAsync().ToTask();
            await bus.PublishAsync(new OrderPlaced { OrderId = "o-1" }, TestContext.Current.CancellationToken);
            Assert.Equal("o-1", (await placed.WaitAsync(Timeout, TestContext.Current.CancellationToken)).OrderId);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldReturnResponsesOverActiveMq()
        {
            using var host = await StartHostAsync("obvs-activemq-requests", service => service
                .HandleRequest<GetQuote, Quote>((request, context, ct) => Task.FromResult(new Quote { Price = 9.99m })));

            var quote = await host.Services.GetRequiredService<IServiceBusClient>()
                .RequestAsync(new GetQuote { Sku = "sku-1" }, Timeout, TestContext.Current.CancellationToken);

            Assert.Equal(9.99m, quote.Price);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldDeliverToEveryBusInTheProcess()
        {
            const string serviceName = "obvs-activemq-every-bus";
            using var hostA = await StartHostAsync(serviceName);
            using var hostB = await StartHostAsync(serviceName);
            var busA = hostA.Services.GetRequiredService<IServiceBus>();
            var busB = hostB.Services.GetRequiredService<IServiceBus>();

            var receivedByA = busA.Commands.OfType<PlaceOrder>().FirstAsync().ToTask();
            var receivedByB = busB.Commands.OfType<PlaceOrder>().FirstAsync().ToTask();
            await busA.SendAsync(new PlaceOrder { OrderId = "o-1" }, TestContext.Current.CancellationToken);

            Assert.Equal("o-1", (await receivedByA.WaitAsync(Timeout, TestContext.Current.CancellationToken)).OrderId);
            Assert.Equal("o-1", (await receivedByB.WaitAsync(Timeout, TestContext.Current.CancellationToken)).OrderId);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldKeepWorkingWhenAnotherHostInTheProcessStops()
        {
            // Two hosts in one process, as tests have: stopping one mustn't close the other's connection
            using var hostA = await StartHostAsync("obvs-activemq-two-hosts");
            var hostB = await StartHostAsync("obvs-activemq-two-hosts");
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
            var host = await StartHostAsync("obvs-activemq-dispose-in-callback");
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
        public async Task ShouldNotDeadlockWhenCodeAfterAwaitingAMessageDisposesTheBus()
        {
            // Code after awaiting a message may shut the app down. If messages arrived on the NMS session's
            // dispatch thread, that code would run there too, and stopping the connection would wait for that
            // same thread, forever. (NMS only skips the wait for code in the dispatch's own execution context,
            // which code after an await is not.)
            var host = await StartHostAsync("obvs-activemq-dispose-after-await");
            var bus = host.Services.GetRequiredService<IServiceBus>();
            // Continuations run inline, wherever the message is received
            var received = new TaskCompletionSource<PlaceOrder>();
            using var subscription = bus.Commands.OfType<PlaceOrder>().Subscribe(command => received.TrySetResult(command));

            async Task DisposeOnReceiving()
            {
                await received.Task.ConfigureAwait(false);
                host.Dispose();
            }

            var disposed = DisposeOnReceiving();
            await bus.SendAsync(new PlaceOrder { OrderId = "o-1" }, TestContext.Current.CancellationToken);

            await disposed.WaitAsync(Timeout, TestContext.Current.CancellationToken);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldContinueTheSendersTraceAcrossActiveMq()
        {
            const string serviceName = "obvs-activemq-tracing";
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
        public async Task ShouldCarryHeadersTheAppSetsOverActiveMq()
        {
            const string serviceName = "obvs-activemq-headers";
            var received = new TaskCompletionSource<IReadOnlyDictionary<string, string>>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var receiver = await StartHostAsync(serviceName, service => service.AsServer()
                .HandleCommand<PlaceOrder>((command, context, ct) =>
                {
                    received.TrySetResult(context.Headers);
                    return Task.CompletedTask;
                }));
            using var sender = await StartHostAsync(serviceName, service => service.AsClient());
            var headers = new Dictionary<string, string> { ["x-tenant"] = "acme" };

            await sender.Services.GetRequiredService<IServiceBusClient>().SendAsync(new PlaceOrder { OrderId = "o-1" }, headers, TestContext.Current.CancellationToken);

            var arrived = await received.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
            Assert.Equal("acme", arrived["x-tenant"]);
        }

        private static async Task<IHost> StartHostAsync(string serviceName, Action<ObvsServiceBuilder> configure = null)
        {
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
            builder.Services.AddObvs(obvs => obvs.AddService<IOrderMessage>(serviceName, service =>
            {
                service
                    .UseActiveMq(options => options.BrokerUri = TestBroker.Uri)
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
