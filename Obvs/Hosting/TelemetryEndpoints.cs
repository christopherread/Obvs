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
    // Wraps a service's endpoints, which know which service a message belongs to, to trace, count and
    // log (at Debug) every outgoing message, and count every incoming command, event and request. The bus
    // shares one subscription to each endpoint's streams among its subscribers, so each is counted once.
    internal sealed class TelemetryServiceEndpoint : IServiceEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse>
    {
        private readonly IServiceEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse> _inner;
        private readonly string _serviceName;
        private readonly ILogger _logger;

        public TelemetryServiceEndpoint(IServiceEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse> inner, string serviceName, ILogger logger)
        {
            _inner = inner;
            _serviceName = serviceName;
            _logger = logger;
            Requests = inner.Requests.CountReceived(serviceName);
            Commands = inner.Commands.CountReceived(serviceName);
        }

        public IObservable<IRequest> Requests { get; }

        public IObservable<ICommand> Commands { get; }

        public Task PublishAsync(IEvent ev, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default)
        {
            return Telemetry.SendAsync("publish", _serviceName, ev, headers, _logger, sent => _inner.PublishAsync(ev, sent, cancellationToken));
        }

        public Task ReplyAsync(IRequest request, IResponse response, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default)
        {
            return Telemetry.SendAsync("reply", _serviceName, response, headers, _logger, sent => _inner.ReplyAsync(request, response, sent, cancellationToken));
        }

        public bool CanHandle(IMessage message) => _inner.CanHandle(message);

        public string Name => _inner.Name;

        public void Dispose() => _inner.Dispose();
    }

    internal sealed class TelemetryServiceEndpointClient : IServiceEndpointClient<IMessage, ICommand, IEvent, IRequest, IResponse>
    {
        private readonly IServiceEndpointClient<IMessage, ICommand, IEvent, IRequest, IResponse> _inner;
        private readonly string _serviceName;
        private readonly ILogger _logger;

        public TelemetryServiceEndpointClient(IServiceEndpointClient<IMessage, ICommand, IEvent, IRequest, IResponse> inner, string serviceName, ILogger logger)
        {
            _inner = inner;
            _serviceName = serviceName;
            _logger = logger;
            Events = inner.Events.CountReceived(serviceName);
        }

        public IObservable<IEvent> Events { get; }

        public Task SendAsync(ICommand command, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default)
        {
            return Telemetry.SendAsync("send", _serviceName, command, headers, _logger, sent => _inner.SendAsync(command, sent, cancellationToken));
        }

        // The inner endpoint sends the request when subscribed to, so the span runs from the subscription until the
        // send finishes. Each request's stream can see every response on the service, as the bus only picks out the
        // request's own after this, so a request counts only its own responses as received.
        public IObservable<IResponse> GetResponses(IRequest request, IReadOnlyDictionary<string, string> headers)
        {
            return Observable.Create<IResponse>(observer =>
            {
                var subscription = new SingleAssignmentDisposable();
                _ = TraceRequestAsync(request, headers, sent =>
                {
                    try
                    {
                        subscription.Disposable = RequestSends.Watch(() => _inner.GetResponses(request, sent)
                            .Do(response =>
                            {
                                if (response.RequestId == request.RequestId && response.RequesterId == request.RequesterId)
                                {
                                    ObvsDiagnostics.RecordReceived(_serviceName, response.GetType());
                                }
                            })
                            .Subscribe(observer), out var sending);
                        return sending;
                    }
                    catch (Exception exception)
                    {
                        observer.OnError(exception);
                        throw;
                    }
                });
                return subscription;
            });
        }

        // Runs synchronously until the send's first await, so the subscription is made before GetResponses returns
        private async Task TraceRequestAsync(IRequest request, IReadOnlyDictionary<string, string> headers, Func<IReadOnlyDictionary<string, string>, Task> subscribeAndSend)
        {
            try
            {
                await Telemetry.SendAsync("request", _serviceName, request, headers, _logger, subscribeAndSend).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Recorded on the span and counted; the response stream has already ended with it
            }
        }

        public bool CanHandle(IMessage message) => _inner.CanHandle(message);

        public string Name => _inner.Name;

        public void Dispose() => _inner.Dispose();
    }

    internal static class Telemetry
    {
        // Sends with the app's headers and the span's trace context
        public static async Task SendAsync(string operation, string serviceName, IMessage message, IReadOnlyDictionary<string, string> headers, ILogger logger,
            Func<IReadOnlyDictionary<string, string>, Task> send)
        {
            using (var activity = ObvsDiagnostics.StartProducer(operation, serviceName, message.GetType()))
            {
                var sent = ObvsDiagnostics.WithTraceContext(activity, headers);
                Log(operation, serviceName, message, logger);
                // Counted once it's sent, or failed to send, as OpenTelemetry's messaging conventions ask
                try
                {
                    await send(sent).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    ObvsDiagnostics.RecordFailure(activity, exception);
                    ObvsDiagnostics.RecordSent(operation, serviceName, message.GetType(), exception);
                    throw;
                }
                ObvsDiagnostics.RecordSent(operation, serviceName, message.GetType());
            }
        }

        private static void Log(string operation, string serviceName, IMessage message, ILogger logger)
        {
            logger.LogDebug("Obvs {Operation} {MessageType} on service '{ServiceName}'.", operation, message.GetType().Name, serviceName);
        }

        public static IObservable<T> CountReceived<T>(this IObservable<T> messages, string serviceName) where T : class
        {
            return messages.Do(message => ObvsDiagnostics.RecordReceived(serviceName, message.GetType()));
        }
    }
}
