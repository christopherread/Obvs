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
    // messages are handled one at a time in arrival order, as an Rx subscriber would see them.
    internal sealed class HandlerRunner
    {
        private readonly IDisposable _subscription;
        private readonly Channel<IMessage> _queue;
        private readonly Task[] _workers;
        private readonly CancellationTokenSource _cancelHandlers = new CancellationTokenSource();

        private HandlerRunner(IDisposable subscription, Channel<IMessage> queue, Func<IMessage, CancellationToken, Task> handle, int maxConcurrency)
        {
            _subscription = subscription;
            _queue = queue;
            _workers = Enumerable.Range(0, maxConcurrency)
                .Select(_ => Task.Run(() => WorkAsync(handle)))
                .ToArray();
        }

        public static HandlerRunner Start<TMessage>(IObservable<TMessage> messages, Func<TMessage, MessageContext, CancellationToken, Task> handler,
            HandlerOptions options, IServiceBus bus, string serviceName, IServiceProvider serviceProvider, ILogger logger)
            where TMessage : class, IMessage
        {
            var queue = Channel.CreateUnbounded<IMessage>(new UnboundedChannelOptions { SingleReader = options.MaxConcurrency == 1 });
            var subscription = messages.Subscribe(
                message => queue.Writer.TryWrite(message),
                error => logger.LogError(error, "The {MessageType} stream on service '{ServiceName}' failed, so its handler stopped receiving.", typeof(TMessage).Name, serviceName));
            var scopes = serviceProvider.GetRequiredService<IServiceScopeFactory>();

            async Task Handle(IMessage message, CancellationToken cancellationToken)
            {
                var started = Stopwatch.GetTimestamp();
                Exception failure = null;
                // The span is current while the handler runs, so anything it sends becomes a child span
                using (var activity = ObvsDiagnostics.StartProcess(serviceName, message.GetType()))
                {
                    var scope = scopes.CreateAsyncScope();
                    try
                    {
                        await handler((TMessage)message, new MessageContext(serviceName, scope.ServiceProvider, bus), cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        // Shutdown ran out of time and cancelled the handler
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
                }
            }

            return new HandlerRunner(subscription, queue, Handle, options.MaxConcurrency);
        }

        // Stops receiving, then lets queued and running messages finish. If stopping runs out of time,
        // running handlers are cancelled and anything still queued is dropped.
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _subscription.Dispose();
            _queue.Writer.TryComplete();

            var finished = Task.WhenAll(_workers);
            var timedOut = Task.Delay(Timeout.Infinite, cancellationToken);
            if (await Task.WhenAny(finished, timedOut).ConfigureAwait(false) == timedOut)
            {
                _cancelHandlers.Cancel();
            }
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
