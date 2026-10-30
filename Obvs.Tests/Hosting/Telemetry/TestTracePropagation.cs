using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Obvs.Hosting;
using Obvs.Testing;
using Obvs.Tests.Hosting.Handlers;
using Obvs.Transports;
using Xunit;

namespace Obvs.Tests.Hosting.Telemetry
{
    // How a transport attaches what it received to the message it delivers, and how apps read it back
    public class TestReceivedMessage
    {
        [Fact]
        public void ShouldReturnTheHeadersAttachedToAReceivedMessage()
        {
            var message = new PlaceOrder("o-1");

            ObvsReceivedMessage.Attach(message, new Dictionary<string, string> { ["x-tenant"] = "acme" });

            Assert.Equal("acme", ObvsMessageHeaders.Get(message)["x-tenant"]);
        }

        [Fact]
        public void ShouldReturnNoHeadersForAMessageWithoutAny()
        {
            Assert.Empty(ObvsMessageHeaders.Get(new PlaceOrder("o-1")));
        }

        [Fact]
        public void ShouldKeepHeadersSeparateForEqualMessages()
        {
            // Records with equal values are equal, but headers belong to the instance
            var first = new PlaceOrder("o-1");
            var second = new PlaceOrder("o-1");

            ObvsReceivedMessage.Attach(first, new Dictionary<string, string> { ["x-tenant"] = "acme" });

            Assert.Empty(ObvsMessageHeaders.Get(second));
        }

        [Fact]
        public void ShouldReturnTheSettlementAttachedToAReceivedMessage()
        {
            var message = new PlaceOrder("o-1");
            var settlement = A.Fake<IMessageSettlement>();

            ObvsReceivedMessage.Attach(message, new Dictionary<string, string>(), settlement);

            Assert.Same(settlement, ObvsReceivedMessage.GetSettlement(message));
        }

        [Fact]
        public void ShouldReturnNoSettlementForAMessageItsTransportSettledOnArrival()
        {
            var message = new PlaceOrder("o-1");

            ObvsReceivedMessage.Attach(message, new Dictionary<string, string>());

            Assert.Null(ObvsReceivedMessage.GetSettlement(message));
        }
    }

    // Two hosts sharing an in-memory broker stand in for two processes
    public class TestTracePropagation
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private readonly string _service = "orders-" + Guid.NewGuid().ToString("N");

        [Fact]
        public async Task ShouldPutTheSendSpansTraceContextInTheHeadersItSends()
        {
            using var activities = new ActivityRecorder();
            await using var harness = await ObvsTestHarness.StartAsync(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>(_service, service => service.UseInMemory())), cancellationToken: Ct);
            var sent = harness.Broker.Messages.FirstAsync().ToTask();

            await harness.Client.SendAsync(new PlaceOrder("o-1"), Ct);

            var send = await WaitForAsync(() => activities.For(_service).SingleOrDefault(activity => activity.DisplayName == $"send {_service}"));
            Assert.Equal($"00-{send.TraceId}-{send.SpanId}-01", (await sent.WaitAsync(Timeout, Ct)).Headers[ObvsMessageHeaders.TraceParent]);
        }

        [Fact]
        public async Task ShouldSendTheAppsHeadersAlongsideTraceContext()
        {
            using var activities = new ActivityRecorder();
            await using var harness = await ObvsTestHarness.StartAsync(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>(_service, service => service.UseInMemory())), cancellationToken: Ct);
            var sent = harness.Broker.Messages.FirstAsync().ToTask();

            await harness.Client.SendAsync(new PlaceOrder("o-1"), new Dictionary<string, string> { ["x-tenant"] = "acme" }, Ct);

            var headers = (await sent.WaitAsync(Timeout, Ct)).Headers;
            Assert.Equal("acme", headers["x-tenant"]);
            Assert.True(headers.ContainsKey(ObvsMessageHeaders.TraceParent));
        }

        [Fact]
        public async Task ShouldNotLetTheAppsHeadersReplaceTheTraceContext()
        {
            // As when a handler passes on the headers it received: the new message continues this trace, not the old
            using var activities = new ActivityRecorder();
            var broker = new InMemoryBroker();
            using var sender = await StartHostAsync(service => service.UseInMemory(broker).AsClient());
            using var receiver = await StartHostAsync(service => service.UseInMemory(broker).AsServer()
                .HandleCommand<PlaceOrder>((command, context, ct) => Task.CompletedTask));
            var stale = new Dictionary<string, string> { [ObvsMessageHeaders.TraceParent] = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01" };

            await sender.Services.GetRequiredService<IServiceBusClient>().SendAsync(new PlaceOrder("o-1"), stale, Ct);

            var send = await WaitForAsync(() => activities.For(_service).SingleOrDefault(activity => activity.DisplayName == $"send {_service}"));
            var process = await WaitForAsync(() => activities.For(_service).SingleOrDefault(activity => activity.DisplayName == $"process {_service}"));
            Assert.Equal(send.TraceId, process.TraceId);
            Assert.Equal(send.SpanId, process.ParentSpanId);
        }

        [Fact]
        public async Task ShouldContinueTheSendersTraceWhenHandling()
        {
            using var activities = new ActivityRecorder();
            var broker = new InMemoryBroker();
            using var sender = await StartHostAsync(service => service.UseInMemory(broker).AsClient());
            using var receiver = await StartHostAsync(service => service.UseInMemory(broker).AsServer()
                .HandleCommand<PlaceOrder>((command, context, ct) => Task.CompletedTask));

            await sender.Services.GetRequiredService<IServiceBusClient>().SendAsync(new PlaceOrder("o-1"), TestContext.Current.CancellationToken);

            var send = await WaitForAsync(() => activities.For(_service).SingleOrDefault(activity => activity.DisplayName == $"send {_service}"));
            var process = await WaitForAsync(() => activities.For(_service).SingleOrDefault(activity => activity.DisplayName == $"process {_service}"));
            Assert.Equal(send.TraceId, process.TraceId);
            Assert.Equal(send.SpanId, process.ParentSpanId);
        }

        [Fact]
        public async Task ShouldContinueTheTraceThroughARequestAndItsReply()
        {
            using var activities = new ActivityRecorder();
            var broker = new InMemoryBroker();
            using var client = await StartHostAsync(service => service.UseInMemory(broker).AsClient());
            using var server = await StartHostAsync(service => service.UseInMemory(broker).AsServer()
                .HandleRequest<GetQuote, Quote>((request, context, ct) => Task.FromResult(new Quote(request.Sku, 9.99m))));

            await client.Services.GetRequiredService<IServiceBusClient>().RequestAsync(new GetQuote("sku-1"), Timeout, Ct);

            var request = await WaitForAsync(() => activities.For(_service).SingleOrDefault(activity => activity.DisplayName == $"request {_service}"));
            var process = await WaitForAsync(() => activities.For(_service).SingleOrDefault(activity => activity.DisplayName == $"process {_service}"));
            var reply = await WaitForAsync(() => activities.For(_service).SingleOrDefault(activity => activity.DisplayName == $"reply {_service}"));
            Assert.Equal(request.SpanId, process.ParentSpanId);
            Assert.Equal(process.SpanId, reply.ParentSpanId);
            Assert.Equal(request.TraceId, reply.TraceId);
        }

        private async Task<IHost> StartHostAsync(Action<ObvsServiceBuilder> configure)
        {
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
            builder.Services.AddObvs(obvs => obvs.AddService<IOrderMessage>(_service, configure));
            var host = builder.Build();
            await host.StartAsync(Ct);
            return host;
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
    }
}
