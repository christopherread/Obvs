using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Obvs
{
    // A stream read with await foreach. Each enumeration subscribes on its own, and buffers up to capacity messages while
    // its reader is busy. A reader that falls further behind gets an exception, rather than holding up the bus's other
    // subscribers or missing messages without knowing.
    internal sealed class ObservableAsyncEnumerable<T> : IAsyncEnumerable<T>
    {
        private readonly IObservable<T> _source;
        private readonly int _capacity;
        private readonly CancellationToken _cancellationToken;

        public ObservableAsyncEnumerable(IObservable<T> source, int capacity, CancellationToken cancellationToken)
        {
            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "A reader's capacity must be at least 1.");
            }
            _source = source;
            _capacity = capacity;
            _cancellationToken = cancellationToken;
        }

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return new ObservableAsyncEnumerator<T>(_source, _capacity, CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken, cancellationToken));
        }
    }

    internal sealed class ObservableAsyncEnumerator<T> : IAsyncEnumerator<T>, IObserver<T>
    {
        private readonly Channel<T> _channel;
        private readonly int _capacity;
        private readonly CancellationTokenSource _cancellation;
        private readonly IDisposable _subscription;

        public ObservableAsyncEnumerator(IObservable<T> source, int capacity, CancellationTokenSource cancellation)
        {
            _capacity = capacity;
            _cancellation = cancellation;
            _channel = Channel.CreateBounded<T>(new BoundedChannelOptions(capacity) { SingleReader = true, SingleWriter = true });
            _subscription = source.Subscribe(this);
        }

        public T Current { get; private set; }

        public ValueTask<bool> MoveNextAsync()
        {
            if (_channel.Reader.TryRead(out var item))
            {
                Current = item;
                return new ValueTask<bool>(true);
            }
            return new ValueTask<bool>(WaitForNextAsync());
        }

        private async Task<bool> WaitForNextAsync()
        {
            try
            {
                while (await _channel.Reader.WaitToReadAsync(_cancellation.Token).ConfigureAwait(false))
                {
                    if (_channel.Reader.TryRead(out var item))
                    {
                        Current = item;
                        return true;
                    }
                }
                return false;
            }
            catch (ChannelClosedException closed) when (closed.InnerException != null)
            {
                // The stream's own error, which the channel wraps
                ExceptionDispatchInfo.Capture(closed.InnerException).Throw();
                throw;
            }
        }

        public void OnNext(T value)
        {
            if (!_channel.Writer.TryWrite(value))
            {
                _channel.Writer.TryComplete(new InvalidOperationException(
                    $"The reader fell more than its capacity of {_capacity} messages behind, so it stopped rather than miss any. " +
                    "Read faster, hand messages off to be processed elsewhere, or pass a larger capacity."));
                _subscription?.Dispose();
            }
        }

        public void OnError(Exception error)
        {
            _channel.Writer.TryComplete(error);
        }

        public void OnCompleted()
        {
            _channel.Writer.TryComplete();
        }

        public ValueTask DisposeAsync()
        {
            _subscription.Dispose();
            _channel.Writer.TryComplete();
            _cancellation.Dispose();
            return default;
        }
    }
}
