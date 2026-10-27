using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Obvs.Hosting;
using Xunit;

namespace Obvs.AmazonSqs.Tests.Hosting
{
    // How many messages a bus handles at once, and how long SQS keeps a message hidden while a handler runs
    public class TestAmazonSqsDelivery : IClassFixture<TestAmazonSqsHosting.ServiceNames>
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);
        private readonly TestAmazonSqsHosting.ServiceNames _serviceNames;

        public TestAmazonSqsDelivery(TestAmazonSqsHosting.ServiceNames serviceNames)
        {
            _serviceNames = serviceNames;
        }

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldHandleUpToMaxConcurrentCallsCommandsAtOnce()
        {
            var running = new ConcurrencyRecorder();
            using var host = await TestAmazonSqsHosting.StartHostAsync(_serviceNames.Create("concurrent"),
                service => service.HandleCommand<PlaceOrder>((command, context, ct) => running.RunAsync(), handler => handler.MaxConcurrency = 3),
                configureOptions: options => options.MaxConcurrentCalls = 3);
            var bus = host.Services.GetRequiredService<IServiceBus>();

            for (var i = 0; i < 3; i++)
            {
                await bus.SendAsync(new PlaceOrder { OrderId = $"o-{i}" }, TestContext.Current.CancellationToken);
            }

            await WaitUntilAsync(() => running.Current == 3);
            running.Finish();
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldHandleOneCommandAtATimeByDefault()
        {
            var running = new ConcurrencyRecorder();
            using var host = await TestAmazonSqsHosting.StartHostAsync(_serviceNames.Create("one-at-a-time"),
                service => service.HandleCommand<PlaceOrder>((command, context, ct) => running.RunAsync(), handler => handler.MaxConcurrency = 3));
            var bus = host.Services.GetRequiredService<IServiceBus>();

            for (var i = 0; i < 3; i++)
            {
                await bus.SendAsync(new PlaceOrder { OrderId = $"o-{i}" }, TestContext.Current.CancellationToken);
            }

            await WaitUntilAsync(() => running.Current == 1);
            await Task.Delay(TimeSpan.FromSeconds(3), Ct);
            Assert.Equal(1, running.Most);
            running.Finish();
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldCreateQueuesWithTheVisibilityTimeout()
        {
            var serviceName = _serviceNames.Create("visibility");
            const string instanceId = "orders-worker-1";

            using var host = await TestAmazonSqsHosting.StartHostAsync(serviceName, instanceId: instanceId, configureOptions: options =>
            {
                options.VisibilityTimeout = TimeSpan.FromSeconds(45);
                options.MaxHandlerDuration = TimeSpan.FromSeconds(30);
            });

            foreach (var queue in new[] { $"{serviceName}-commands", $"{serviceName}-requests", $"{serviceName}-events-{instanceId}", $"{serviceName}-responses-{instanceId}" })
            {
                Assert.Equal("45", (await TestBroker.QueueAttributesAsync(queue, Ct))["VisibilityTimeout"]);
            }
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldKeepACommandHiddenWhileItsHandlerRunsWithinTheVisibilityTimeout()
        {
            var serviceName = _serviceNames.Create("hidden");
            var attempts = 0;
            using var host = await TestAmazonSqsHosting.StartHostAsync(serviceName,
                service => service.HandleCommand<PlaceOrder>(async (command, context, ct) =>
                {
                    Interlocked.Increment(ref attempts);
                    await Task.Delay(TimeSpan.FromSeconds(3), ct);
                }, handler => handler.MaxConcurrency = 2),
                configureOptions: options =>
                {
                    options.VisibilityTimeout = TimeSpan.FromSeconds(6);
                    options.MaxHandlerDuration = TimeSpan.FromSeconds(4);
                    options.MaxConcurrentCalls = 2;
                });

            await host.Services.GetRequiredService<IServiceBus>().SendAsync(new PlaceOrder { OrderId = "o-1" }, TestContext.Current.CancellationToken);

            await WaitUntilAsync(() => Volatile.Read(ref attempts) == 1);
            await Task.Delay(TimeSpan.FromSeconds(9), Ct);
            Assert.Equal(1, Volatile.Read(ref attempts));
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldRedeliverACommandWhoseHandlerOutlastsTheVisibilityTimeout()
        {
            // Why VisibilityTimeout must exceed MaxHandlerDuration: SQS doesn't know a handler is still running
            var serviceName = _serviceNames.Create("outlasted");
            var attempts = 0;
            using var host = await TestAmazonSqsHosting.StartHostAsync(serviceName,
                service => service.HandleCommand<PlaceOrder>(async (command, context, ct) =>
                {
                    if (Interlocked.Increment(ref attempts) == 1)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(8), ct);
                    }
                }, handler => handler.MaxConcurrency = 2),
                configureOptions: options =>
                {
                    options.VisibilityTimeout = TimeSpan.FromSeconds(2);
                    options.MaxHandlerDuration = TimeSpan.FromSeconds(1);
                    options.MaxConcurrentCalls = 2;
                });

            await host.Services.GetRequiredService<IServiceBus>().SendAsync(new PlaceOrder { OrderId = "o-1" }, TestContext.Current.CancellationToken);

            await WaitUntilAsync(() => Volatile.Read(ref attempts) >= 2);
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
