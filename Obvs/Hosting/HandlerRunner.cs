using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Obvs.Types;

namespace Obvs.Hosting
{
    internal enum HandlerKind
    {
        Command,
        Event,
        Request,
    }

    // What a HandleCommand/HandleEvent/HandleRequest call registered, until the host starts it
    internal sealed class HandlerRegistration
    {
        public HandlerRegistration(HandlerKind kind, Type messageType, HandlerOptions options, Func<IServiceBus, string, IServiceProvider, ILogger, HandlerRunner> start)
        {
            Kind = kind;
            MessageType = messageType;
            Options = options;
            Start = start;
        }

        public HandlerKind Kind { get; }
        public Type MessageType { get; }
        public HandlerOptions Options { get; }
        public Func<IServiceBus, string, IServiceProvider, ILogger, HandlerRunner> Start { get; }
    }

    // Queues messages from the bus and runs the handler on them, MaxConcurrency at a time. With one worker,
    // messages are handled one at a time in arrival order, as an Rx subscriber would see them. Each message is
    // settled with its transport once handled: completed, abandoned if the handler threw, or released if the
    // runner stopped before handling it.
    internal sealed class HandlerRunner
    {
        private readonly IDisposable _subscription;
        private readonly Channel<IMessage> _queue;
        private readonly Func<IMessage, Task> _release;
        private readonly Task[] _workers;
        private readonly CancellationTokenSource _cancelHandlers = new CancellationTokenSource();

        private HandlerRunner(IDisposable subscription, Channel<IMessage> queue, Func<IMessage, CancellationToken, Task> handle, Func<IMessage, Task> release, int maxConcurrency)
        {
            _subscription = subscription;
            _queue = queue;
            _release = release;
            _workers = Enumerable.Range(0, maxConcurrency)
                .Select(_ => Task.Run(() => WorkAsync(handle)))
                .ToArray();
        }

        public static HandlerRunner Start<TMessage>(IObservable<TMessage> messages, Func<TMessage, MessageContext, CancellationToken, Task> handler,
            HandlerOptions options, IServiceBus bus, string serviceName, IServiceProvider serviceProvider, ILogger logger)
            where TMessage : class, IMessage
        {
            var queue = Channel.CreateUnbounded<IMessage>(new UnboundedChannelOptions { SingleReader = options.MaxConcurrency == 1 });
            Task Release(IMessage message) => Settlement.ReleaseAsync(message, serviceName, logger);
            // Once the runner is stopping its queue takes no more, so messages still arriving go back to the transport
            var subscription = messages.Subscribe(
                message =>
                {
                    if (!queue.Writer.TryWrite(message))
                    {
                        _ = Release(message);
                    }
                },
                error => logger.LogError(error, "The {MessageType} stream on service '{ServiceName}' failed, so its handler stopped receiving.", typeof(TMessage).Name, serviceName));
            var scopes = serviceProvider.GetRequiredService<IServiceScopeFactory>();

            async Task Handle(IMessage message, CancellationToken cancellationToken)
            {
                var started = Stopwatch.GetTimestamp();
                Exception failure = null;
                // The span is current while the handler runs, so anything it sends becomes a child span
                using (var activity = ObvsDiagnostics.StartProcess(serviceName, message.GetType(), ObvsDiagnostics.ExtractParent(message)))
                {
                    var scope = scopes.CreateAsyncScope();
                    var cancelled = false;
                    try
                    {
                        await handler((TMessage)message, new MessageContext(serviceName, ObvsMessageHeaders.Get(message), scope.ServiceProvider, bus), cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        // Shutdown ran out of time and cancelled the handler
                        cancelled = true;
                    }
                    catch (Exception exception)
                    {
                        failure = exception;
                        ObvsDiagnostics.RecordFailure(activity, exception);
                        logger.LogError(exception, "Handling {MessageType} on service '{ServiceName}' failed.", message.GetType().Name, serviceName);
                    }
                    finally
                    {
                        await scope.DisposeAsync().ConfigureAwait(false);
                        ObvsDiagnostics.RecordProcessed(serviceName, message.GetType(), started, failure);
                    }

                    // A request's handler has already sent its reply, so the request is only completed once it has
                    await (cancelled ? Release(message)
                        : failure != null ? Settlement.AbandonAsync(message, failure, serviceName, logger)
                        : Settlement.CompleteAsync(message, serviceName, logger)).ConfigureAwait(false);
                }
            }

            return new HandlerRunner(subscription, queue, Handle, Release, options.MaxConcurrency);
        }

        // Stops taking messages, then lets queued and running ones finish and settle before unsubscribing, as some
        // transports can only settle a message while it's subscribed. If stopping runs out of time, running
        // handlers are cancelled, and they and anything still queued are released.
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _queue.Writer.TryComplete();

            var finished = Task.WhenAll(_workers);
            var timedOut = Task.Delay(Timeout.Infinite, cancellationToken);
            if (await Task.WhenAny(finished, timedOut).ConfigureAwait(false) == timedOut)
            {
                _cancelHandlers.Cancel();
                while (_queue.Reader.TryRead(out var message))
                {
                    await _release(message).ConfigureAwait(false);
                }
            }

            _subscription.Dispose();
        }

        private async Task WorkAsync(Func<IMessage, CancellationToken, Task> handle)
        {
            while (await _queue.Reader.WaitToReadAsync().ConfigureAwait(false))
            {
                while (!_cancelHandlers.IsCancellationRequested && _queue.Reader.TryRead(out var message))
                {
                    await handle(message, _cancelHandlers.Token).ConfigureAwait(false);
                }
                if (_cancelHandlers.IsCancellationRequested)
                {
                    return;
                }
            }
        }
    }
}
