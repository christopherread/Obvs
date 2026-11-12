using System;
using System.Threading;
using System.Threading.Tasks;

namespace Obvs.AmazonSqs
{
    // While the bus runs, records every LastSeenInterval on its own queues that it's still running, recreating them and
    // its subscription if they've gone, then deletes the queues crashed buses of the service left behind. A refresh or
    // sweep that fails is tried again at the next interval; AbandonedAfter allows for several missed refreshes.
    internal sealed class BusHousekeeping : IDisposable
    {
        private readonly AmazonSqsEntities _entities;
        private readonly TimeSpan _interval;
        private readonly CancellationTokenSource _stopping = new CancellationTokenSource();
        private readonly Random _random = new Random(Guid.NewGuid().GetHashCode());
        private Task _running;
        private int _disposed;

        public BusHousekeeping(AmazonSqsEntities entities, TimeSpan interval)
        {
            _entities = entities;
            _interval = interval;
        }

        public void Start()
        {
            _running = Task.Run(() => RunAsync(_stopping.Token));
        }

        private async Task RunAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                // Between three quarters of the interval and all of it, so the service's buses don't all sweep together
                // and none refreshes later than its interval
                await Task.Delay(TimeSpan.FromTicks((long)(_interval.Ticks * (0.75 + 0.25 * _random.NextDouble()))), ct).ConfigureAwait(false);
                await TryAsync(() => _entities.RefreshBusEntitiesAsync(ct), ct).ConfigureAwait(false);
                await TryAsync(() => _entities.SweepAbandonedAsync(ct), ct).ConfigureAwait(false);
            }
        }

        private static async Task TryAsync(Func<Task> action, CancellationToken ct)
        {
            try
            {
                await action().ConfigureAwait(false);
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
            }
        }

        // Waits for a refresh in progress, so the bus's queues aren't recreated after it deletes them
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
            {
                return;
            }
            _stopping.Cancel();
            try
            {
                _running?.GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
            }
            _stopping.Dispose();
        }
    }
}
