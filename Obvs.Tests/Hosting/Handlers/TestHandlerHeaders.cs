using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Obvs.Hosting;
using Obvs.Testing;
using Xunit;

namespace Obvs.Tests.Hosting.Handlers
{
    // Headers travel as an argument to each send, and a handler reads the ones its message arrived with
    public class TestHandlerHeaders
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private static readonly IReadOnlyDictionary<string, string> Acme = new Dictionary<string, string> { ["x-tenant"] = "acme" };

        [Fact]
        public async Task ShouldGiveACommandHandlerTheHeadersItsSenderSet()
        {
            var received = new TaskCompletionSource<IReadOnlyDictionary<string, string>>(TaskCreationOptions.RunContinuationsAsynchronously);
            await using var harness = await StartOrdersAsync(service => service
                .HandleCommand<PlaceOrder>((command, context, ct) =>
                {
                    received.TrySetResult(context.Headers);
                    return Task.CompletedTask;
                }));

            await harness.Client.SendAsync(new PlaceOrder("o-1"), Acme, Ct);

            Assert.Equal("acme", (await received.Task.WaitAsync(Timeout, Ct))["x-tenant"]);
        }

        [Fact]
        public async Task ShouldGiveAnEventHandlerTheHeadersItsPublisherSet()
        {
            var received = new TaskCompletionSource<IReadOnlyDictionary<string, string>>(TaskCreationOptions.RunContinuationsAsynchronously);
            await using var harness = await StartOrdersAsync(service => service
                .HandleEvent<OrderPlaced>((placed, context, ct) =>
                {
                    received.TrySetResult(context.Headers);
                    return Task.CompletedTask;
                }));

            await harness.Bus.PublishAsync(new OrderPlaced("o-1"), Acme, Ct);

            Assert.Equal("acme", (await received.Task.WaitAsync(Timeout, Ct))["x-tenant"]);
        }

        [Fact]
        public async Task ShouldGiveARequestHandlerTheHeadersItsRequesterSet()
        {
            await using var harness = await StartOrdersAsync(service => service
                .HandleRequest<GetQuote, Quote>((request, context, ct) => Task.FromResult(new Quote(context.Headers["x-tenant"], 9.99m))));

            var quote = await harness.Client.RequestAsync(new GetQuote("sku-1"), Acme, Timeout, Ct);

            Assert.Equal("acme", quote.Sku);
        }

        [Fact]
        public async Task ShouldGiveRxSubscribersTheHeadersAMessageArrivedWith()
        {
            await using var harness = await StartOrdersAsync(service => { });
            var received = harness.Bus.Commands.OfType<PlaceOrder>().FirstAsync().ToTask();

            await harness.Client.SendAsync(new PlaceOrder("o-1"), Acme, Ct);

            Assert.Equal("acme", ObvsMessageHeaders.Get(await received.WaitAsync(Timeout, Ct))["x-tenant"]);
        }

        [Fact]
        public async Task ShouldSendEachCallsOwnHeadersWhenOneMessageIsSentTwiceAtOnce()
        {
            // Headers kept on the message object would be shared by both sends
            await using var harness = await StartOrdersAsync(service => { });
            var sent = harness.Broker.Messages.Take(2).ToList().ToTask();
            var command = new PlaceOrder("o-1");

            await Task.WhenAll(
                harness.Client.SendAsync(command, new Dictionary<string, string> { ["x-tenant"] = "acme" }, Ct),
                harness.Client.SendAsync(command, new Dictionary<string, string> { ["x-tenant"] = "globex" }, Ct));

            var tenants = (await sent.WaitAsync(Timeout, Ct)).Select(message => message.Headers["x-tenant"]).OrderBy(tenant => tenant);
            Assert.Equal(new[] { "acme", "globex" }, tenants);
        }

        [Fact]
        public async Task ShouldNotAttachHeadersToTheMessageTheAppSends()
        {
            // A client with no server on the broker, so nothing receives the same object back
            await using var harness = await StartOrdersAsync(service => service.AsClient());
            var command = new PlaceOrder("o-1");

            await harness.Client.SendAsync(command, Acme, Ct);

            Assert.Empty(ObvsMessageHeaders.Get(command));
        }

        [Fact]
        public async Task ShouldLetAHandlerPassHeadersOn()
        {
            await using var harness = await StartOrdersAsync(service => service
                .HandleCommand<PlaceOrder>((command, context, ct) => context.PublishAsync(new OrderPlaced(command.OrderId), context.Headers, ct)));
            var published = harness.Broker.Messages.FirstAsync(message => message.Message is OrderPlaced).ToTask();

            await harness.Client.SendAsync(new PlaceOrder("o-1"), Acme, Ct);

            Assert.Equal("acme", (await published.WaitAsync(Timeout, Ct)).Headers["x-tenant"]);
        }

        [Fact]
        public async Task ShouldLetAHandlerAwaitAResponse()
        {
            await using var harness = await StartOrdersAsync(service => service
                .HandleRequest<GetQuote, Quote>((request, context, ct) => Task.FromResult(new Quote(request.Sku, 9.99m)))
                .HandleCommand<PlaceOrder>(async (command, context, ct) =>
                {
                    var quote = await context.RequestAsync(new GetQuote(command.OrderId), Timeout, ct);
                    await context.PublishAsync(new OrderPlaced($"{command.OrderId}:{quote.Price}"), ct);
                }));

            await harness.Client.SendAsync(new PlaceOrder("o-1"), Ct);

            await harness.Published<OrderPlaced>(placed => placed.OrderId == "o-1:9.99");
        }

        private static Task<ObvsTestHarness> StartOrdersAsync(Action<ObvsServiceBuilder> configure)
        {
            return ObvsTestHarness.StartAsync(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => configure(service.UseInMemory()))), cancellationToken: Ct);
        }
    }
}
