using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using Obvs.Hosting;
using Xunit;

namespace Obvs.Testing.Tests
{
    public class TestObvsTestHarness
    {
        [Fact]
        public async Task ShouldReturnAMessagePublishedAfterWaitingStarts()
        {
            await using var harness = await StartOrdersHarnessAsync();

            var waiting = harness.Published<OrderPlaced>();
            await harness.Bus.PublishAsync(new OrderPlaced("o-1"));

            Assert.Equal("o-1", (await waiting).OrderId);
        }

        [Fact]
        public async Task ShouldReturnAMessagePublishedBeforeWaitingStarts()
        {
            await using var harness = await StartOrdersHarnessAsync();
            await harness.Bus.PublishAsync(new OrderPlaced("o-1"));

            Assert.Equal("o-1", (await harness.Published<OrderPlaced>()).OrderId);
        }

        [Fact]
        public async Task ShouldReturnOnlyAMessageMatchingThePredicate()
        {
            await using var harness = await StartOrdersHarnessAsync();
            await harness.Bus.PublishAsync(new OrderPlaced("o-1"));
            await harness.Bus.PublishAsync(new OrderPlaced("o-2"));

            Assert.Equal("o-2", (await harness.Published<OrderPlaced>(placed => placed.OrderId == "o-2")).OrderId);
        }

        [Fact]
        public async Task ShouldTestAServiceEndToEnd()
        {
            await using var harness = await StartOrdersHarnessAsync();
            using var service = harness.Bus.Commands.OfType<PlaceOrder>()
                .Subscribe(command => _ = harness.Bus.PublishAsync(new OrderPlaced(command.OrderId)));

            await harness.Client.SendAsync(new PlaceOrder("o-1"));

            await harness.Published<OrderPlaced>(placed => placed.OrderId == "o-1");
        }

        [Fact]
        public async Task ShouldSeeCommandsSentByAClientOnlyApp()
        {
            await using var harness = await ObvsTestHarness.StartAsync(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service.UseInMemory().AsClient())),
                cancellationToken: TestContext.Current.CancellationToken);

            await harness.Client.SendAsync(new PlaceOrder("o-1"));

            Assert.Equal("o-1", (await harness.Sent<PlaceOrder>()).OrderId);
        }

        [Fact]
        public async Task ShouldSeeRequestsAndReplies()
        {
            await using var harness = await StartOrdersHarnessAsync();
            using var server = harness.Bus.Requests.OfType<GetQuote>()
                .Subscribe(request => _ = harness.Bus.ReplyAsync(request, new Quote(request.Sku, 9.99m)));

            using var requester = harness.Client.GetResponses(new GetQuote("sku-1")).Subscribe(_ => { });

            Assert.Equal("sku-1", (await harness.Requested<GetQuote>()).Sku);
            Assert.Equal(9.99m, (await harness.Replied<Quote>(quote => quote.Sku == "sku-1")).Price);
        }

        [Fact]
        public async Task ShouldRecordEveryMessageInOrder()
        {
            await using var harness = await StartOrdersHarnessAsync();
            await harness.Client.SendAsync(new PlaceOrder("o-1"));
            await harness.Bus.PublishAsync(new OrderPlaced("o-1"));
            await harness.Published<OrderPlaced>();

            Assert.Equal(new object[] { new PlaceOrder("o-1"), new OrderPlaced("o-1") }, harness.Messages);
        }

        [Fact]
        public async Task ShouldListTheMessagesSeenWhenWaitingTimesOut()
        {
            await using var harness = await StartOrdersHarnessAsync();
            await harness.Bus.PublishAsync(new OrderPlaced("o-1"));
            await harness.Published<OrderPlaced>();

            var exception = await Assert.ThrowsAsync<ObvsTestTimeoutException>(
                () => harness.Published<OrderPlaced>(placed => placed.OrderId == "o-2", TimeSpan.FromMilliseconds(100)));

            Assert.Contains("OrderPlaced", exception.Message);
            Assert.Contains("o-1", exception.Message);
        }

        [Fact]
        public async Task ShouldUseTheDefaultTimeoutFromTheOptions()
        {
            await using var harness = await ObvsTestHarness.StartAsync(
                services => services.AddObvs(obvs => obvs.AddService<IOrderMessage>("orders", service => service.UseInMemory())),
                options => options.DefaultTimeout = TimeSpan.FromMilliseconds(100),
                TestContext.Current.CancellationToken);

            await Assert.ThrowsAsync<ObvsTestTimeoutException>(() => harness.Published<OrderPlaced>())
                .WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        }

        [Fact]
        public async Task ShouldTimeOutOnTheTimeProvider()
        {
            var time = new FakeTimeProvider();
            await using var harness = await ObvsTestHarness.StartAsync(
                services => services.AddObvs(obvs => obvs.AddService<IOrderMessage>("orders", service => service.UseInMemory())),
                options => options.TimeProvider = time,
                TestContext.Current.CancellationToken);

            var waiting = harness.Published<OrderPlaced>(timeout: TimeSpan.FromMinutes(5));
            time.Advance(TimeSpan.FromMinutes(6));

            // Completes because virtual time passed, long before five real minutes
            await Assert.ThrowsAsync<ObvsTestTimeoutException>(() => waiting)
                .WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        }

        [Fact]
        public async Task ShouldStopTheHostWhenDisposed()
        {
            var harness = await StartOrdersHarnessAsync();
            var lifetime = harness.Services.GetRequiredService<IHostApplicationLifetime>();

            await harness.DisposeAsync();

            Assert.True(lifetime.ApplicationStopped.IsCancellationRequested);
        }

        private static Task<ObvsTestHarness> StartOrdersHarnessAsync()
        {
            return ObvsTestHarness.StartAsync(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service.UseInMemory())),
                cancellationToken: TestContext.Current.CancellationToken);
        }
    }
}
