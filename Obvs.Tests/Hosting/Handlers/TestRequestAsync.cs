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

            var quote = await harness.Client.RequestAsync<GetQuote, Quote>(new GetQuote("sku-1"), TimeSpan.FromSeconds(5), Ct);

            Assert.Equal(9.99m, quote.Price);
        }

        [Fact]
        public async Task ShouldTimeOutOnTheTimeProviderWhenNoResponseArrives()
        {
            var time = new FakeTimeProvider();
            await using var harness = await StartOrdersAsync();

            var pending = harness.Client.RequestAsync<GetQuote, Quote>(new GetQuote("sku-1"), TimeSpan.FromMinutes(5), Ct, time);
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

            var pending = harness.Client.RequestAsync<GetQuote, Quote>(new GetQuote("sku-1"), TimeSpan.FromMinutes(5), cancellation.Token);
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending).WaitAsync(TimeSpan.FromSeconds(2), Ct);
        }

        private static Task<ObvsTestHarness> StartOrdersAsync()
        {
            return ObvsTestHarness.StartAsync(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service.UseInMemory())),
                cancellationToken: Ct);
        }
    }
}
