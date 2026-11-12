using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NetMQ;
using NetMQ.Sockets;
using Obvs.Hosting;
using Xunit;

namespace Obvs.NetMQ.Tests.Hosting
{
    // End to end through AddObvs and UseNetMq, over TCP on localhost. Each test has its own service name and its own
    // four ports (Port to Port + 3), clear of the other NetMQ tests.
    public class TestNetMqHosting
    {
        private const string Address = "tcp://localhost";
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldSendCommandsAndPublishEventsOverNetMq()
        {
            using var server = await StartHostAsync("obvs-netmq-commands-events", 5700, service => service.AsServer()
                .HandleCommand<PlaceOrder>((command, context, ct) => context.PublishAsync(new OrderPlaced { OrderId = command.OrderId }, ct)));
            using var client = await StartHostAsync("obvs-netmq-commands-events", 5700, service => service.AsClient());
            var bus = client.Services.GetRequiredService<IServiceBusClient>();
            var placed = bus.Events.OfType<OrderPlaced>().FirstAsync().ToTask();

            await bus.SendAsync(new PlaceOrder { OrderId = "o-1" }, TestContext.Current.CancellationToken);

            Assert.Equal("o-1", (await placed.WaitAsync(Timeout, TestContext.Current.CancellationToken)).OrderId);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldReturnResponsesOverNetMq()
        {
            using var server = await StartHostAsync("obvs-netmq-requests", 5710, service => service.AsServer()
                .HandleRequest<GetQuote, Quote>((request, context, ct) => Task.FromResult(new Quote { Price = 9.99m })));
            using var client = await StartHostAsync("obvs-netmq-requests", 5710, service => service.AsClient());

            var quote = await client.Services.GetRequiredService<IServiceBusClient>()
                .RequestAsync(new GetQuote { Sku = "sku-1" }, Timeout, TestContext.Current.CancellationToken);

            Assert.Equal(9.99m, quote.Price);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldNotDeadlockWhenASubscriberDisposesTheBus()
        {
            // The subscriber runs on the message source's receive thread, which disposing the bus stops
            var host = await StartHostAsync("obvs-netmq-dispose-in-callback", 5760, service => { });
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
        public async Task ShouldPublishMessagesAV6AppCanRead()
        {
            // A v6 receiver reads exactly four frames: topic, type name, data type and body. By default v7 sends the
            // same, even for a message with headers.
            const string serviceName = "obvs-netmq-v6-reads";
            using var server = await StartHostAsync(serviceName, 5720, service => service.AsServer());
            using var v6Subscriber = new SubscriberSocket();
            v6Subscriber.Connect($"{Address}:5723");
            v6Subscriber.Subscribe($"{serviceName}.Events");
            var headers = new Dictionary<string, string> { ["x-tenant"] = "acme" };

            await server.Services.GetRequiredService<IServiceBus>().PublishAsync(new OrderPlaced { OrderId = "o-1" }, headers, TestContext.Current.CancellationToken);

            var message = new NetMQMessage();
            Assert.True(v6Subscriber.TryReceiveMultipartMessage(Timeout, ref message));
            Assert.Equal(new[] { $"{serviceName}.Events", "OrderPlaced", "Byte[]" }, message.Take(3).Select(frame => frame.ConvertToString()));
            Assert.Equal(4, message.FrameCount);
            Assert.Contains("o-1", message[3].ConvertToString());
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldReceiveMessagesAV6AppSends()
        {
            const string serviceName = "obvs-netmq-v6-sends";
            var received = new TaskCompletionSource<PlaceOrder>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var server = await StartHostAsync(serviceName, 5730, service => service.AsServer()
                .HandleCommand<PlaceOrder>((command, context, ct) =>
                {
                    received.TrySetResult(command);
                    return Task.CompletedTask;
                }));
            using var v6Publisher = new XPublisherSocket();
            v6Publisher.Connect($"{Address}:5732");
            // The server's subscription arrives once connected; a message sent before it would be dropped
            Assert.True(v6Publisher.TryReceiveFrameBytes(Timeout, out _));

            v6Publisher.SendMultipartMessage(new NetMQMessage(new[]
            {
                new NetMQFrame($"{serviceName}.Commands"),
                new NetMQFrame("PlaceOrder"),
                new NetMQFrame("Byte[]"),
                new NetMQFrame(Encoding.UTF8.GetBytes(@"{""OrderId"":""o-2""}")),
            }));

            Assert.Equal("o-2", (await received.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken)).OrderId);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldCarryHeadersTheAppSetsWhenSendingHeaders()
        {
            const string serviceName = "obvs-netmq-headers";
            var received = new TaskCompletionSource<IReadOnlyDictionary<string, string>>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var server = await StartHostAsync(serviceName, 5740, service => service.AsServer()
                .HandleCommand<PlaceOrder>((command, context, ct) =>
                {
                    received.TrySetResult(context.Headers);
                    return Task.CompletedTask;
                }), sendHeaders: true);
            using var client = await StartHostAsync(serviceName, 5740, service => service.AsClient(), sendHeaders: true);
            var headers = new Dictionary<string, string> { ["x-tenant"] = "acme" };

            await client.Services.GetRequiredService<IServiceBusClient>().SendAsync(new PlaceOrder { OrderId = "o-1" }, headers, TestContext.Current.CancellationToken);

            var arrived = await received.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
            Assert.Equal("acme", arrived["x-tenant"]);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldContinueTheSendersTraceWhenSendingHeaders()
        {
            const string serviceName = "obvs-netmq-tracing";
            using var activities = new ActivityRecorder();
            using var server = await StartHostAsync(serviceName, 5750, service => service.AsServer()
                .HandleCommand<PlaceOrder>((command, context, ct) => Task.CompletedTask), sendHeaders: true);
            using var client = await StartHostAsync(serviceName, 5750, service => service.AsClient(), sendHeaders: true);

            await client.Services.GetRequiredService<IServiceBusClient>().SendAsync(new PlaceOrder { OrderId = "o-1" }, TestContext.Current.CancellationToken);

            var send = await WaitForAsync(() => activities.For(serviceName).SingleOrDefault(activity => activity.DisplayName == $"send {serviceName}"));
            var process = await WaitForAsync(() => activities.For(serviceName).SingleOrDefault(activity => activity.DisplayName == $"process {serviceName}"));
            Assert.Equal(send.TraceId, process.TraceId);
            Assert.Equal(send.SpanId, process.ParentSpanId);
        }

        // No serializer is set, so these use the System.Text.Json default
        private static async Task<IHost> StartHostAsync(string serviceName, int port, Action<ObvsServiceBuilder> configure, bool sendHeaders = false)
        {
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
            builder.Services.AddObvs(obvs => obvs.AddService<IOrderMessage>(serviceName, service => configure(service.UseNetMq(options =>
            {
                options.Address = Address;
                options.Port = port;
                options.SendHeaders = sendHeaders;
            }))));
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
