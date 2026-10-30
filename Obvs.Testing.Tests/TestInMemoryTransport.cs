using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Reactive.Testing;
using Obvs.Hosting;
using Obvs.Types;
using Xunit;

namespace Obvs.Testing.Tests
{
    // Exercises the transport on its own, through AddObvs and the bus, without the harness
    public class TestInMemoryTransport
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

        [Fact]
        public async Task ShouldDeliverCommandsFromClientToServer()
        {
            using var host = await StartHostAsync(obvs => obvs.AddService<IOrderMessage>("orders", service => service.UseInMemory()));
            var bus = host.Services.GetRequiredService<IServiceBus>();

            var received = bus.Commands.OfType<PlaceOrder>().FirstAsync().ToTask();
            await bus.SendAsync(new PlaceOrder("o-1"), TestContext.Current.CancellationToken);

            Assert.Equal("o-1", (await received.WaitAsync(Timeout, TestContext.Current.CancellationToken)).OrderId);
        }

        [Fact]
        public async Task ShouldDeliverEventsFromServerToClient()
        {
            using var host = await StartHostAsync(obvs => obvs.AddService<IOrderMessage>("orders", service => service.UseInMemory()));
            var bus = host.Services.GetRequiredService<IServiceBus>();

            var received = bus.Events.OfType<OrderPlaced>().FirstAsync().ToTask();
            await bus.PublishAsync(new OrderPlaced("o-1"), TestContext.Current.CancellationToken);

            Assert.Equal("o-1", (await received.WaitAsync(Timeout, TestContext.Current.CancellationToken)).OrderId);
        }

        [Fact]
        public async Task ShouldReturnResponsesToTheRequester()
        {
            using var host = await StartHostAsync(obvs => obvs.AddService<IOrderMessage>("orders", service => service.UseInMemory()));
            var bus = host.Services.GetRequiredService<IServiceBus>();
            using var server = bus.Requests.OfType<GetQuote>()
                .Subscribe(request => _ = bus.ReplyAsync(request, new Quote(request.Sku, 9.99m)));

            var quote = await bus.GetResponses(new GetQuote("sku-1")).OfType<Quote>().FirstAsync().ToTask()
                .WaitAsync(Timeout, TestContext.Current.CancellationToken);

            Assert.Equal(new Quote("sku-1", 9.99m), quote with { RequestId = null, RequesterId = null });
        }

        [Fact]
        public async Task ShouldOnlyDeliverMessagesToTheirOwnService()
        {
            // The bus merges every service's commands, so a command delivered to both services would arrive twice.
            // The test scheduler makes delivery deterministic, so "exactly once" needs no waiting.
            var scheduler = new TestScheduler();
            var broker = new InMemoryBroker(scheduler);
            using var host = await StartHostAsync(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service.UseInMemory(broker))
                .AddService<IShippingMessage>("shipping", service => service.UseInMemory(broker)));
            var bus = host.Services.GetRequiredService<IServiceBus>();

            var received = new List<ICommand>();
            using var subscription = bus.Commands.Subscribe(received.Add);
            await bus.SendAsync(new ShipOrder("o-1"), TestContext.Current.CancellationToken);
            scheduler.Start();

            Assert.Equal(new ICommand[] { new ShipOrder("o-1") }, received);
        }

        [Fact]
        public async Task ShouldDeliverMessagesInTheOrderTheyWereSent()
        {
            using var host = await StartHostAsync(obvs => obvs.AddService<IOrderMessage>("orders", service => service.UseInMemory()));
            var bus = host.Services.GetRequiredService<IServiceBus>();
            var ids = Enumerable.Range(1, 100).Select(i => $"o-{i}").ToArray();

            var received = bus.Commands.OfType<PlaceOrder>().Take(ids.Length).Select(command => command.OrderId).ToArray().ToTask();
            foreach (var id in ids)
            {
                await bus.SendAsync(new PlaceOrder(id), TestContext.Current.CancellationToken);
            }

            Assert.Equal(ids, await received.WaitAsync(Timeout, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task ShouldGiveEachContainerItsOwnBroker()
        {
            using var hostA = await StartHostAsync(obvs => obvs.AddService<IOrderMessage>("orders", service => service.UseInMemory()));
            using var hostB = await StartHostAsync(obvs => obvs.AddService<IOrderMessage>("orders", service => service.UseInMemory()));
            var busA = hostA.Services.GetRequiredService<IServiceBus>();
            var busB = hostB.Services.GetRequiredService<IServiceBus>();

            Assert.NotSame(hostA.Services.GetRequiredService<InMemoryBroker>(), hostB.Services.GetRequiredService<InMemoryBroker>());

            var receivedByB = busB.Commands.OfType<PlaceOrder>().FirstAsync().ToTask();
            var receivedByA = busA.Commands.OfType<PlaceOrder>().FirstAsync().ToTask();
            await busA.SendAsync(new PlaceOrder("o-1"), TestContext.Current.CancellationToken);

            await receivedByA.WaitAsync(Timeout, TestContext.Current.CancellationToken);
            Assert.False(receivedByB.IsCompleted);
        }

        [Fact]
        public async Task ShouldConnectHostsThatShareABroker()
        {
            var broker = new InMemoryBroker();
            using var clientHost = await StartHostAsync(obvs => obvs.AddService<IOrderMessage>("orders", service => service.UseInMemory(broker).AsClient()));
            using var serverHost = await StartHostAsync(obvs => obvs.AddService<IOrderMessage>("orders", service => service.UseInMemory(broker).AsServer()));

            var received = serverHost.Services.GetRequiredService<IServiceBus>().Commands.OfType<PlaceOrder>().FirstAsync().ToTask();
            await clientHost.Services.GetRequiredService<IServiceBusClient>().SendAsync(new PlaceOrder("o-1"), TestContext.Current.CancellationToken);

            Assert.Equal("o-1", (await received.WaitAsync(Timeout, TestContext.Current.CancellationToken)).OrderId);
        }

        [Fact]
        public async Task ShouldDeliverOnTheBrokersScheduler()
        {
            var scheduler = new TestScheduler();
            var broker = new InMemoryBroker(scheduler);
            using var host = await StartHostAsync(obvs => obvs.AddService<IOrderMessage>("orders", service => service.UseInMemory(broker)));
            var bus = host.Services.GetRequiredService<IServiceBus>();

            PlaceOrder received = null;
            using var subscription = bus.Commands.OfType<PlaceOrder>().Subscribe(command => received = command);
            await bus.SendAsync(new PlaceOrder("o-1"), TestContext.Current.CancellationToken);

            Assert.Null(received);

            scheduler.Start();

            Assert.Equal("o-1", received?.OrderId);
        }

        [Fact]
        public async Task ShouldRecordTheHeadersEachMessageWasSentWith()
        {
            var broker = new InMemoryBroker();
            using var host = await StartHostAsync(obvs => obvs.AddService<IOrderMessage>("orders", service => service.UseInMemory(broker)));
            var sent = broker.Messages.FirstAsync().ToTask();

            await host.Services.GetRequiredService<IServiceBus>()
                .SendAsync(new PlaceOrder("o-1"), new Dictionary<string, string> { ["x-tenant"] = "acme" }, TestContext.Current.CancellationToken);

            Assert.Equal("acme", (await sent.WaitAsync(Timeout, TestContext.Current.CancellationToken)).Headers["x-tenant"]);
        }

        [Fact]
        public async Task ShouldDeliverTheHeadersAMessageWasSentWith()
        {
            // As a broker would, so tests on the in-memory transport see what handlers see in production
            using var host = await StartHostAsync(obvs => obvs.AddService<IOrderMessage>("orders", service => service.UseInMemory()));
            var bus = host.Services.GetRequiredService<IServiceBus>();
            var received = bus.Events.OfType<OrderPlaced>().FirstAsync().ToTask();

            await bus.PublishAsync(new OrderPlaced("o-1"), new Dictionary<string, string> { ["x-tenant"] = "acme" }, TestContext.Current.CancellationToken);

            Assert.Equal("acme", ObvsMessageHeaders.Get(await received.WaitAsync(Timeout, TestContext.Current.CancellationToken))["x-tenant"]);
        }

        private static async Task<IHost> StartHostAsync(Action<ObvsBuilder> configure)
        {
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
            builder.Services.AddObvs(configure);
            var host = builder.Build();
            await host.StartAsync(TestContext.Current.CancellationToken);
            return host;
        }
    }
}
