using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Obvs.Hosting;
using Obvs.Testing;
using Obvs.Tests.Hosting.Handlers;
using Xunit;

namespace Obvs.Tests.Hosting.Telemetry
{
    public class TestMessageHeaders
    {
        [Fact]
        public void ShouldReturnTheHeadersSetOnAMessage()
        {
            var message = new PlaceOrder("o-1");

            ObvsMessageHeaders.Set(message, new Dictionary<string, string> { ["x-tenant"] = "acme" });

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

            ObvsMessageHeaders.Set(first, new Dictionary<string, string> { ["x-tenant"] = "acme" });

            Assert.Empty(ObvsMessageHeaders.Get(second));
        }
    }

    // Two hosts sharing an in-memory broker stand in for two processes
    public class TestTracePropagation
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private readonly string _service = "orders-" + Guid.NewGuid().ToString("N");

        [Fact]
        public async Task ShouldPutTheSendSpansTraceContextInTheMessagesHeaders()
        {
            using var activities = new ActivityRecorder();
            await using var harness = await ObvsTestHarness.StartAsync(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>(_service, service => service.UseInMemory())), cancellationToken: Ct);
            var command = new PlaceOrder("o-1");

            await harness.Client.SendAsync(command, TestContext.Current.CancellationToken);

            var send = await WaitForAsync(() => activities.For(_service).SingleOrDefault(activity => activity.DisplayName == $"send {_service}"));
            Assert.Equal($"00-{send.TraceId}-{send.SpanId}-01", ObvsMessageHeaders.Get(command)[ObvsMessageHeaders.TraceParent]);
        }

        [Fact]
        public async Task ShouldKeepHeadersTheAppSetWhenAddingTraceContext()
        {
            using var activities = new ActivityRecorder();
            await using var harness = await ObvsTestHarness.StartAsync(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>(_service, service => service.UseInMemory())), cancellationToken: Ct);
            var command = new PlaceOrder("o-1");
            ObvsMessageHeaders.Set(command, new Dictionary<string, string> { ["x-tenant"] = "acme" });

            await harness.Client.SendAsync(command, TestContext.Current.CancellationToken);

            var headers = ObvsMessageHeaders.Get(command);
            Assert.Equal("acme", headers["x-tenant"]);
            Assert.True(headers.ContainsKey(ObvsMessageHeaders.TraceParent));
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

            await client.Services.GetRequiredService<IServiceBusClient>().RequestAsync<GetQuote, Quote>(new GetQuote("sku-1"), Timeout, Ct);

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
