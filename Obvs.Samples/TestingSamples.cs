using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Microsoft.Reactive.Testing;
using Obvs.Hosting;
using Obvs.Testing;
using Xunit;

namespace Obvs.Samples
{
    // docs/testing.md, run as tests. The docs leave out xUnit's cancellation token, for brevity.
#pragma warning disable xUnit1051
    public class TestingSamples
    {
        // snippet: test-a-handler
        [Fact]
        public async Task PlacingAnOrderPublishesOrderPlaced()
        {
            await using var harness = await ObvsTestHarness.StartAsync(services => services
                .AddSingleton<IOrderStore, InMemoryOrderStore>()
                .AddObvs(obvs => obvs.AddService<IOrderMessage>("orders", service => service
                    .UseInMemory()
                    .HandleCommand<PlaceOrder, PlaceOrderHandler>())));

            await harness.Client.SendAsync(new PlaceOrder("o-1"));

            var placed = await harness.Published<OrderPlaced>(e => e.OrderId == "o-1");
            Assert.Equal("o-1", placed.OrderId);
        }
        // end-snippet

        [Fact]
        public async Task AskingForAQuoteGetsOne()
        {
            await using var harness = await ObvsTestHarness.StartAsync(services => services
                .AddObvs(obvs => obvs.AddService<IOrderMessage>("orders", service => service
                    .UseInMemory()
                    .HandleRequest<GetQuote, Quote>((request, context, ct) => Task.FromResult(new Quote(request.Sku, 9.99m))))));

            // snippet: test-a-request
            var quote = await harness.Client.RequestAsync<GetQuote, Quote>(new GetQuote("sku-1"), TimeSpan.FromSeconds(5));
            // end-snippet

            Assert.Equal(9.99m, quote.Price);
        }

        [Fact]
        public async Task WaitingTimesOutInVirtualTime()
        {
            // snippet: test-with-time
            var time = new FakeTimeProvider();
            await using var harness = await ObvsTestHarness.StartAsync(
                services => services.AddObvs(obvs => obvs.AddService<IOrderMessage>("orders", service => service.UseInMemory())),
                options =>
                {
                    options.TimeProvider = time;             // the harness's timeouts
                    options.Scheduler = new TestScheduler(); // where the broker delivers, for Rx's virtual time
                });
            // end-snippet

            var waiting = harness.Published<OrderPlaced>(timeout: TimeSpan.FromSeconds(5));
            time.Advance(TimeSpan.FromSeconds(5));

            await Assert.ThrowsAsync<ObvsTestTimeoutException>(() => waiting);
        }
    }
#pragma warning restore xUnit1051
}
