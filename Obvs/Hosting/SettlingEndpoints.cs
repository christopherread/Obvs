using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Obvs.Transports;
using Obvs.Types;

namespace Obvs.Hosting
{
    // Wraps a service's endpoints to deliver each message to the bus's subscribers as a Delivery, which settles the
    // message once the handlers and SubscribeAsyncs it reaches have processed it, or as soon as it's delivered if none do.
    internal sealed class SettlingServiceEndpoint : IServiceEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse>
    {
        private readonly IServiceEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse> _inner;

        public SettlingServiceEndpoint(IServiceEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse> inner, string serviceName, ILogger logger)
        {
            _inner = inner;
            Requests = inner.Requests.SettleOnceProcessed(serviceName, logger);
            Commands = inner.Commands.SettleOnceProcessed(serviceName, logger);
        }

        public IObservable<IRequest> Requests { get; }

        public IObservable<ICommand> Commands { get; }

        public Task PublishAsync(IEvent ev, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default) => _inner.PublishAsync(ev, headers, cancellationToken);

        public Task ReplyAsync(IRequest request, IResponse response, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default) => _inner.ReplyAsync(request, response, headers, cancellationToken);

        public bool CanHandle(IMessage message) => _inner.CanHandle(message);

        public string Name => _inner.Name;

        public void Dispose() => _inner.Dispose();
    }

    internal sealed class SettlingServiceEndpointClient : IServiceEndpointClient<IMessage, ICommand, IEvent, IRequest, IResponse>
    {
        private readonly IServiceEndpointClient<IMessage, ICommand, IEvent, IRequest, IResponse> _inner;
        private readonly string _serviceName;
        private readonly ILogger _logger;

        public SettlingServiceEndpointClient(IServiceEndpointClient<IMessage, ICommand, IEvent, IRequest, IResponse> inner, string serviceName, ILogger logger)
        {
            _inner = inner;
            _serviceName = serviceName;
            _logger = logger;
            Events = inner.Events.SettleOnceProcessed(serviceName, logger);
        }

        public IObservable<IEvent> Events { get; }

        public Task SendAsync(ICommand command, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default) => _inner.SendAsync(command, headers, cancellationToken);

        public IObservable<IResponse> GetResponses(IRequest request, IReadOnlyDictionary<string, string> headers) => _inner.GetResponses(request, headers).SettleOnceProcessed(_serviceName, _logger);

        public bool CanHandle(IMessage message) => _inner.CanHandle(message);

        public string Name => _inner.Name;

        public void Dispose() => _inner.Dispose();
    }

    internal static class SettlingObservableExtensions
    {
        public static IObservable<T> SettleOnceProcessed<T>(this IObservable<T> messages, string serviceName, ILogger logger)
            where T : class
        {
            return Observable.Create<T>(observer =>
            {
                var subscription = new SettlingSubscription<T>(observer, serviceName, logger);
                subscription.Subscribe(messages);
                return subscription;
            });
        }

        // Delivers each message as a Delivery. Unsubscribing while a message is being delivered, as FirstAsync does,
        // waits until the delivery has started settling it, as a transport may stop settling messages once unsubscribed.
        private sealed class SettlingSubscription<T> : IObserver<T>, IDisposable
            where T : class
        {
            private readonly IObserver<T> _observer;
            private readonly string _serviceName;
            private readonly ILogger _logger;
            private readonly SingleAssignmentDisposable _source = new SingleAssignmentDisposable();
            private readonly object _gate = new object();
            private bool _delivering;
            private bool _disposed;

            public SettlingSubscription(IObserver<T> observer, string serviceName, ILogger logger)
            {
                _observer = observer;
                _serviceName = serviceName;
                _logger = logger;
            }

            public void Subscribe(IObservable<T> messages)
            {
                _source.Disposable = messages.Subscribe(this);
            }

            public void OnNext(T message)
            {
                lock (_gate)
                {
                    _delivering = true;
                }
                try
                {
                    Delivery.Deliver(message, _observer, _serviceName, _logger);
                }
                finally
                {
                    bool disposed;
                    lock (_gate)
                    {
                        _delivering = false;
                        disposed = _disposed;
                    }
                    if (disposed)
                    {
                        _source.Dispose();
                    }
                }
            }

            public void OnError(Exception error) => _observer.OnError(error);

            public void OnCompleted() => _observer.OnCompleted();

            public void Dispose()
            {
                lock (_gate)
                {
                    _disposed = true;
                    if (_delivering)
                    {
                        return;
                    }
                }
                _source.Dispose();
            }
        }
    }
}
