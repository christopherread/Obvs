using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Obvs.NATS.Tests.Hosting;
using Xunit;
using static Obvs.NATS.Tests.JetStream.JetStreamServices;

namespace Obvs.NATS.Tests.JetStream
{
    // How many messages a bus takes from each consumer at once
    public class TestNatsJetStreamDelivery : IClassFixture<JetStreamServices>
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);
        private readonly JetStreamServices _services;

        public TestNatsJetStreamDelivery(JetStreamServices services)
        {
            _services = services;
        }

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldHandleAsManyCommandsAtOnceAsTheirHandlersMaxConcurrency()
        {
            var running = new ConcurrencyRecorder();
            using var host = await StartHostAsync(_services.Create("concurrent"), service => service
                .HandleCommand<PlaceOrder>((command, context, ct) => running.RunAsync(), handler => handler.MaxConcurrency = 4));
            var bus = host.Services.GetRequiredService<IServiceBus>();

            for (var i = 0; i < 4; i++)
            {
                await bus.SendAsync(new PlaceOrder { OrderId = $"o-{i}" }, TestContext.Current.CancellationToken);
            }

            await WaitUntilAsync(() => running.Current == 4);
            running.Finish();
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldHandleNoMoreCommandsAtOnceThanMaxConcurrentMessages()
        {
            var running = new ConcurrencyRecorder();
            using var host = await StartHostAsync(_services.Create("capped"), service => service
                .HandleCommand<PlaceOrder>((command, context, ct) => running.RunAsync(), handler => handler.MaxConcurrency = 4),
                options: options => options.MaxConcurrentMessages = 2);
            var bus = host.Services.GetRequiredService<IServiceBus>();

            for (var i = 0; i < 4; i++)
            {
                await bus.SendAsync(new PlaceOrder { OrderId = $"o-{i}" }, TestContext.Current.CancellationToken);
            }

            await WaitUntilAsync(() => running.Current == 2);
            await Task.Delay(TimeSpan.FromSeconds(3), Ct);
            Assert.Equal(2, running.Most);
            running.Finish();
        }

        private static async Task WaitUntilAsync(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow + Timeout;
            while (!condition())
            {
                if (DateTime.UtcNow > deadline)
                {
                    throw new TimeoutException("The condition wasn't met in time.");
                }
                await Task.Delay(100, Ct);
            }
        }

        // Counts handlers running at once, holding each until Finish
        private sealed class ConcurrencyRecorder
        {
            private readonly TaskCompletionSource<bool> _finish = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            private int _current;
            private int _most;

            public int Current => Volatile.Read(ref _current);

            public int Most => Volatile.Read(ref _most);

            public async Task RunAsync()
            {
                var now = Interlocked.Increment(ref _current);
                int most;
                while ((most = Volatile.Read(ref _most)) < now && Interlocked.CompareExchange(ref _most, now, most) != most)
                {
                }
                try
                {
                    await _finish.Task;
                }
                finally
                {
                    Interlocked.Decrement(ref _current);
                }
            }

            public void Finish() => _finish.TrySetResult(true);
        }
    }
}
