using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Obvs.Types;

namespace Obvs
{
    /// <summary>
    /// A bus's streams read with <c>await foreach</c>, at the reader's own pace:
    /// <c>await foreach (var placed in client.ReadEventsAsync&lt;OrderPlaced&gt;(ct)) { ... }</c>.
    /// </summary>
    public static class ServiceBusAsyncEnumerableExtensions
    {
        /// <summary>How many messages a reader can fall behind by, unless it says otherwise.</summary>
        public const int DefaultCapacity = 1024;

        /// <summary>
        /// The bus's events of type <typeparamref name="TEvent"/>, from when enumeration starts until it's cancelled or
        /// stopped. Each enumeration subscribes on its own, and buffers up to <paramref name="capacity"/> events while its
        /// reader is busy. A reader that falls further behind gets an <see cref="InvalidOperationException"/>, rather than
        /// holding up the bus's other subscribers or missing events without knowing.
        /// </summary>
        public static IAsyncEnumerable<TEvent> ReadEventsAsync<TEvent>(this IServiceBusClient client,
            CancellationToken cancellationToken = default, int capacity = DefaultCapacity)
            where TEvent : IEvent
        {
            return new ObservableAsyncEnumerable<TEvent>(client.Events.OfType<TEvent>(), capacity, cancellationToken);
        }

        /// <summary>
        /// The bus's commands of type <typeparamref name="TCommand"/>, read as <see cref="ReadEventsAsync{TEvent}"/> reads
        /// events. A command handler (<c>HandleCommand</c>) does more for you: it settles each command, and runs in a scope.
        /// </summary>
        public static IAsyncEnumerable<TCommand> ReadCommandsAsync<TCommand>(this IServiceBus bus,
            CancellationToken cancellationToken = default, int capacity = DefaultCapacity)
            where TCommand : ICommand
        {
            return new ObservableAsyncEnumerable<TCommand>(bus.Commands.OfType<TCommand>(), capacity, cancellationToken);
        }

        private sealed class ObservableAsyncEnumerable<T> : IAsyncEnumerable<T>
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
                return new Enumerator<T>(_source, _capacity, CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken, cancellationToken));
            }
        }

        private sealed class Enumerator<T> : IAsyncEnumerator<T>, IObserver<T>
        {
            private readonly Channel<T> _channel;
            private readonly int _capacity;
            private readonly CancellationTokenSource _cancellation;
            private readonly IDisposable _subscription;

            public Enumerator(IObservable<T> source, int capacity, CancellationTokenSource cancellation)
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
}
