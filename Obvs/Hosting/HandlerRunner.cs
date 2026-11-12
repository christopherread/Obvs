using System;
using System.Threading;
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

    // Runs a handler on each message of its type, MaxConcurrency at a time and each in a DI scope of its own, settling
    // each message once handled. It's the processor SubscribeAsync uses, registered at startup and stopped with the host.
    internal sealed class HandlerRunner
    {
        private readonly Func<CancellationToken, Task> _stop;

        private HandlerRunner(Func<CancellationToken, Task> stop)
        {
            _stop = stop;
        }

        public static HandlerRunner Start<TMessage>(IObservable<TMessage> messages, Func<TMessage, MessageContext, CancellationToken, Task> handler,
            HandlerOptions options, IServiceBus bus, string serviceName, IServiceProvider serviceProvider, ILogger logger)
            where TMessage : class, IMessage
        {
            var scopes = serviceProvider.GetRequiredService<IServiceScopeFactory>();

            async Task Handle(TMessage message, CancellationToken cancellationToken)
            {
                var scope = scopes.CreateAsyncScope();
                try
                {
                    await handler(message, new MessageContext(serviceName, ObvsMessageHeaders.Get(message), scope.ServiceProvider, bus), cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    await scope.DisposeAsync().ConfigureAwait(false);
                }
            }

            var processor = new MessageProcessor<TMessage>(messages, Handle, options.MaxConcurrency, onError: null, logger,
                error => logger.LogError(error, "The {MessageType} stream on service '{ServiceName}' failed, so its handler stopped receiving.", typeof(TMessage).Name, serviceName));
            return new HandlerRunner(processor.StopAsync);
        }

        // Stops taking messages, then lets queued and running ones finish and settle. If stopping runs out of time,
        // running handlers are cancelled, and they and anything still queued are released.
        public Task StopAsync(CancellationToken cancellationToken)
        {
            return _stop(cancellationToken);
        }
    }
}
