using System;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Obvs.Hosting;
using Obvs.Testing;
using Xunit;

namespace Obvs.Tests.Hosting.Handlers
{
    // RequestAsync works on any bus; these use a plain Rx responder rather than a request handler
    public class TestRequestAsync
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        [Fact]
        public async Task ShouldReturnTheResponse()
        {
            await using var harness = await StartOrdersAsync();
            using var server = harness.Bus.Requests.OfType<GetQuote>()
                .Subscribe(request => _ = harness.Bus.ReplyAsync(request, new Quote(request.Sku, 9.99m)));

            var quote = await harness.Client.RequestAsync(new GetQuote("sku-1"), TimeSpan.FromSeconds(5), Ct);

            Assert.Equal(9.99m, quote.Price);
        }

        [Fact]
        public async Task ShouldReturnTheResponseTypeTheRequestDeclares()
        {
            await using var harness = await StartOrdersAsync();
            using var server = harness.Bus.Requests.OfType<GetQuote>()
                .Subscribe(request => _ = harness.Bus.ReplyAsync(request, new Quote(request.Sku, 9.99m)));

            // GetQuote is an IRequest<Quote>, so the compiler infers Quote
            Quote quote = await harness.Client.RequestAsync(new GetQuote("sku-1"), TimeSpan.FromSeconds(5), Ct);

            Assert.Equal("sku-1", quote.Sku);
        }

        [Fact]
        public async Task ShouldTimeOutOnTheTimeProviderWhenNoResponseArrives()
        {
            var time = new FakeTimeProvider();
            await using var harness = await StartOrdersAsync();

            var pending = harness.Client.RequestAsync(new GetQuote("sku-1"), TimeSpan.FromMinutes(5), Ct, time);
            time.Advance(TimeSpan.FromMinutes(6));

            // Completes because virtual time passed, long before five real minutes
            var exception = await Assert.ThrowsAsync<TimeoutException>(() => pending).WaitAsync(TimeSpan.FromSeconds(2), Ct);
            Assert.Contains("No Quote response to GetQuote", exception.Message);
        }

        [Fact]
        public async Task ShouldStopWaitingWhenCancelled()
        {
            await using var harness = await StartOrdersAsync();
            using var cancellation = new CancellationTokenSource();

            var pending = harness.Client.RequestAsync(new GetQuote("sku-1"), TimeSpan.FromMinutes(5), cancellation.Token);
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending).WaitAsync(TimeSpan.FromSeconds(2), Ct);
        }

        [Fact]
        public async Task ShouldThrowTheSendFailureRatherThanTimingOut()
        {
            var transport = new RequestTransport { SendFailure = new InvalidOperationException("The broker refused the request") };
            await using var harness = await StartClientAsync(transport);

            var pending = harness.Client.RequestAsync(new GetQuote("sku-1"), TimeSpan.FromMinutes(5), Ct);

            // Long before the five-minute timeout
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => pending).WaitAsync(TimeSpan.FromSeconds(2), Ct);
            Assert.Equal("The broker refused the request", exception.Message);
        }

        [Fact]
        public async Task ShouldSendTheNextRequestAfterOneFailsToSend()
        {
            var transport = new RequestTransport { SendFailure = new InvalidOperationException("The broker refused the request") };
            await using var harness = await StartClientAsync(transport);
            await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Client.RequestAsync(new GetQuote("sku-1"), TimeSpan.FromMinutes(5), Ct))
                .WaitAsync(TimeSpan.FromSeconds(2), Ct);

            transport.SendFailure = null;
            var quote = await harness.Client.RequestAsync(new GetQuote("sku-2"), TimeSpan.FromSeconds(5), Ct);

            Assert.Equal("sku-2", quote.Sku);
        }

        [Fact]
        public async Task ShouldReceiveAResponseThatArrivesDuringTheSend()
        {
            await using var harness = await StartClientAsync(new RequestTransport());

            var quote = await harness.Client.RequestAsync(new GetQuote("sku-1"), TimeSpan.FromSeconds(5), Ct);

            Assert.Equal("sku-1", quote.Sku);
        }

        private static Task<ObvsTestHarness> StartClientAsync(RequestTransport transport)
        {
            return ObvsTestHarness.StartAsync(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service.AsClient().UseEndpointProvider(_ => transport))),
                cancellationToken: Ct);
        }

        private static Task<ObvsTestHarness> StartOrdersAsync()
        {
            return ObvsTestHarness.StartAsync(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service.UseInMemory())),
                cancellationToken: Ct);
        }
    }
}
