using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Obvs.Hosting
{
    // Queues messages from a stream and awaits process on them, maxConcurrency at a time. With one worker, messages are
    // processed one at a time in arrival order, as an Rx subscriber would see them. A failure is recorded and passed to
    // onError without ending the subscription. Each message claimed from the bus's delivery is settled once processed:
    // completed, abandoned if processing failed, or released if the processor stopped first. Handlers and
    // SubscribeAsync both run on it.
    internal sealed class MessageProcessor<T> : IDisposable
    {
        private readonly Func<T, CancellationToken, Task> _process;
        private readonly Action<T, Exception> _onError;
        private readonly ILogger _logger;
        private readonly Channel<Queued> _queue;
        private readonly CancellationTokenSource _cancel = new CancellationTokenSource();
        private readonly Task[] _workers;
        private readonly IDisposable _subscription;
        private readonly object _gate = new object();
        private Task _stopping;

        // logger, if given, logs failures in place of the bus's logger for messages
        public MessageProcessor(IObservable<T> messages, Func<T, CancellationToken, Task> process, int maxConcurrency,
            Action<T, Exception> onError, ILogger logger = null, Action<Exception> onStreamError = null)
        {
            _process = process;
            _onError = onError;
            _logger = logger;
            _queue = Channel.CreateUnbounded<Queued>(new UnboundedChannelOptions { SingleReader = maxConcurrency == 1 });
            _workers = Enumerable.Range(0, maxConcurrency)
                .Select(_ => Task.Run(() => WorkAsync()))
                .ToArray();
            _subscription = messages.Subscribe(Enqueue, onStreamError ?? (_ => { }));
        }

        // Stops taking messages, then lets queued and running ones finish and settle before unsubscribing, as some
        // transports can only settle a message while it's subscribed. If stopping runs out of time, running ones are
        // cancelled, and they and anything still queued are released.
        public Task StopAsync(CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                return _stopping ?? (_stopping = StopOnceAsync(cancellationToken));
            }
        }

        // Stops without waiting: what's running is cancelled, and it and what's queued go back to the transport
        public void Dispose()
        {
            _cancel.Cancel();
            _ = StopAsync(new CancellationToken(true));
        }

        private async Task StopOnceAsync(CancellationToken cancellationToken)
        {
            _queue.Writer.TryComplete();

            var finished = Task.WhenAll(_workers);
            var timedOut = Task.Delay(Timeout.Infinite, cancellationToken);
            if (await Task.WhenAny(finished, timedOut).ConfigureAwait(false) == timedOut)
            {
                _cancel.Cancel();
            }
            while (_queue.Reader.TryRead(out var queued))
            {
                await Release(queued).ConfigureAwait(false);
            }

            _subscription.Dispose();
        }

        private void Enqueue(T message)
        {
            var queued = new Queued(message, Delivery.Claim(message));
            // Once stopping, it takes no more, so messages still arriving go back to the transport
            if (!_queue.Writer.TryWrite(queued))
            {
                _ = Release(queued);
            }
        }

        private static Task Release(Queued queued)
        {
            return queued.Delivery?.ReleaseAsync(null, true) ?? Task.CompletedTask;
        }

        private async Task WorkAsync()
        {
            while (await _queue.Reader.WaitToReadAsync().ConfigureAwait(false))
            {
                while (!_cancel.IsCancellationRequested && _queue.Reader.TryRead(out var queued))
                {
                    await ProcessAsync(queued, _cancel.Token).ConfigureAwait(false);
                }
                if (_cancel.IsCancellationRequested)
                {
                    return;
                }
            }
        }

        private async Task ProcessAsync(Queued queued, CancellationToken cancellationToken)
        {
            var message = queued.Message;
            var delivery = queued.Delivery;
            var started = Stopwatch.GetTimestamp();
            Exception failure = null;
            var stopped = false;
            // The span is current while the message is processed, so anything sent meanwhile becomes a child span
            using (var activity = delivery == null ? null : ObvsDiagnostics.StartProcess(delivery.ServiceName, message.GetType(), ObvsDiagnostics.ExtractParent(message)))
            {
                try
                {
                    await _process(message, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // Stopping ran out of time and cancelled it
                    stopped = true;
                }
                catch (Exception exception)
                {
                    failure = exception;
                    ReportFailure(message, delivery, activity, exception);
                }

                if (delivery != null)
                {
                    ObvsDiagnostics.RecordProcessed(delivery.ServiceName, message.GetType(), started, failure);
                    // A request's handler has already sent its reply, so the request is only completed once it has
                    await delivery.ReleaseAsync(failure, stopped).ConfigureAwait(false);
                }
            }
        }

        private void ReportFailure(T message, Delivery delivery, Activity activity, Exception exception)
        {
            var logger = _logger ?? delivery?.Logger;
            if (delivery != null)
            {
                ObvsDiagnostics.RecordFailure(activity, exception);
                logger.LogError(exception, "Processing {MessageType} on service '{ServiceName}' failed.", message.GetType().Name, delivery.ServiceName);
            }
            if (_onError == null)
            {
                return;
            }

            try
            {
                _onError(message, exception);
            }
            catch (Exception onErrorFailure)
            {
                // It mustn't stop later messages being processed
                logger?.LogError(onErrorFailure, "The onError callback for {MessageType} failed.", message.GetType().Name);
            }
        }

        private sealed class Queued
        {
            public Queued(T message, Delivery delivery)
            {
                Message = message;
                Delivery = delivery;
            }

            public T Message { get; }

            // Null if the message wasn't claimed from the bus's delivery, so there's nothing to settle
            public Delivery Delivery { get; }
        }
    }
}
