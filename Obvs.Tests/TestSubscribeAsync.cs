using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Obvs.Tests
{
    // SubscribeAsync awaits each message: one at a time in order, or up to maxConcurrency at once
    public class TestSubscribeAsync
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        [Fact]
        public async Task ShouldProcessMessagesOneAtATimeInOrderByDefault()
        {
            var messages = new Subject<int>();
            var log = new ConcurrentQueue<string>();
            var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var subscription = messages.SubscribeAsync(async (message, ct) =>
            {
                log.Enqueue($"start {message}");
                await Task.Delay(10, ct);
                log.Enqueue($"end {message}");
                if (message == 5)
                {
                    done.TrySetResult(true);
                }
            });

            foreach (var message in Enumerable.Range(1, 5))
            {
                messages.OnNext(message);
            }
            await done.Task.WaitAsync(Timeout, Ct);

            Assert.Equal(Enumerable.Range(1, 5).SelectMany(message => new[] { $"start {message}", $"end {message}" }), log);
        }

        [Fact]
        public async Task ShouldProcessUpToMaxConcurrencyMessagesAtOnce()
        {
            var messages = new Subject<int>();
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var gate = new object();
            int running = 0, peak = 0, processed = 0;
            using var subscription = messages.SubscribeAsync(async (message, ct) =>
            {
                lock (gate)
                {
                    peak = Math.Max(peak, ++running);
                }
                await release.Task;
                lock (gate)
                {
                    running--;
                    processed++;
                }
            }, maxConcurrency: 3);

            foreach (var message in Enumerable.Range(1, 5))
            {
                messages.OnNext(message);
            }
            await WaitUntilAsync(() => Volatile.Read(ref running) == 3);
            await Task.Delay(100, Ct); // A fourth would have started by now

            Assert.Equal(3, Volatile.Read(ref running));
            release.SetResult(true);
            await WaitUntilAsync(() => Volatile.Read(ref processed) == 5);
            Assert.Equal(3, peak);
        }

        [Fact]
        public async Task ShouldCarryOnAfterAMessageFailsAndPassTheFailureToOnError()
        {
            var messages = new Subject<int>();
            var processed = new ConcurrentQueue<int>();
            var failures = new ConcurrentQueue<(int Message, Exception Exception)>();
            using var subscription = messages.SubscribeAsync((message, ct) =>
                {
                    if (message == 2)
                    {
                        throw new InvalidOperationException("Out of stock");
                    }
                    processed.Enqueue(message);
                    return Task.CompletedTask;
                },
                onError: (message, exception) => failures.Enqueue((message, exception)));

            messages.OnNext(1);
            messages.OnNext(2);
            messages.OnNext(3);
            await WaitUntilAsync(() => processed.Count == 2);

            Assert.Equal(new[] { 1, 3 }, processed);
            var failure = Assert.Single(failures);
            Assert.Equal(2, failure.Message);
            Assert.Equal("Out of stock", failure.Exception.Message);
        }

        [Fact]
        public async Task ShouldTakeADelegateWithoutACancellationToken()
        {
            var messages = new Subject<int>();
            var processed = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var subscription = messages.SubscribeAsync(async message =>
            {
                await Task.Yield();
                processed.TrySetResult(message);
            });

            messages.OnNext(7);

            Assert.Equal(7, await processed.Task.WaitAsync(Timeout, Ct));
        }

        [Fact]
        public async Task ShouldCancelWhatsRunningAndTakeNoMoreWhenDisposed()
        {
            var messages = new Subject<int>();
            var processed = new ConcurrentQueue<int>();
            var failures = new ConcurrentQueue<Exception>();
            var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var subscription = messages.SubscribeAsync(async (message, ct) =>
                {
                    processed.Enqueue(message);
                    started.TrySetResult(true);
                    using (ct.Register(() => cancelled.TrySetResult(true)))
                    {
                        await Task.Delay(System.Threading.Timeout.Infinite, ct);
                    }
                },
                onError: (message, exception) => failures.Enqueue(exception));
            messages.OnNext(1);
            messages.OnNext(2);
            await started.Task.WaitAsync(Timeout, Ct);

            subscription.Dispose();
            await cancelled.Task.WaitAsync(Timeout, Ct);
            messages.OnNext(3);
            await Task.Delay(100, Ct);

            // 1 was cancelled, 2 was queued and dropped, and 3 arrived too late; being stopped isn't a failure
            Assert.Equal(new[] { 1 }, processed);
            Assert.Empty(failures);
        }

        [Fact]
        public void ShouldCheckItsArguments()
        {
            var messages = new Subject<int>();

            Assert.Throws<ArgumentNullException>(() => ((IObservable<int>)null).SubscribeAsync((message, ct) => Task.CompletedTask));
            Assert.Throws<ArgumentNullException>(() => messages.SubscribeAsync((Func<int, CancellationToken, Task>)null));
            Assert.Throws<ArgumentNullException>(() => messages.SubscribeAsync((Func<int, Task>)null));
            var error = Assert.Throws<ArgumentOutOfRangeException>(() => messages.SubscribeAsync((message, ct) => Task.CompletedTask, maxConcurrency: 0));
            Assert.Equal("maxConcurrency", error.ParamName);
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
                await Task.Delay(10, Ct);
            }
        }
    }
}
