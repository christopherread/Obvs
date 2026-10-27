using System;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
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

        public Task PublishAsync(IEvent ev, CancellationToken cancellationToken = default)
        {
            return Telemetry.SendAsync("publish", _serviceName, ev, _logger, () => _inner.PublishAsync(ev, cancellationToken));
        }

        public Task ReplyAsync(IRequest request, IResponse response, CancellationToken cancellationToken = default)
        {
            return Telemetry.SendAsync("reply", _serviceName, response, _logger, () => _inner.ReplyAsync(request, response, cancellationToken));
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

        public Task SendAsync(ICommand command, CancellationToken cancellationToken = default)
        {
            return Telemetry.SendAsync("send", _serviceName, command, _logger, () => _inner.SendAsync(command, cancellationToken));
        }

        // The inner endpoint sends the request when subscribed to, so the span covers the subscription. Responses
        // aren't counted as received: each request's stream can see every response on the service, as the bus
        // only picks out the request's own after this.
        public IObservable<IResponse> GetResponses(IRequest request)
        {
            return Observable.Create<IResponse>(observer =>
            {
                using (var activity = ObvsDiagnostics.StartProducer("request", _serviceName, request.GetType()))
                {
                    ObvsDiagnostics.InjectTraceContext(activity, request);
                    Telemetry.LogAndCount("request", _serviceName, request, _logger);
                    try
                    {
                        return _inner.GetResponses(request).Subscribe(observer);
                    }
                    catch (Exception exception)
                    {
                        ObvsDiagnostics.RecordFailure(activity, exception);
                        throw;
                    }
                }
            });
        }

        public bool CanHandle(IMessage message) => _inner.CanHandle(message);

        public string Name => _inner.Name;

        public void Dispose() => _inner.Dispose();
    }

    internal static class Telemetry
    {
        public static async Task SendAsync(string operation, string serviceName, IMessage message, ILogger logger, Func<Task> send)
        {
            using (var activity = ObvsDiagnostics.StartProducer(operation, serviceName, message.GetType()))
            {
                ObvsDiagnostics.InjectTraceContext(activity, message);
                LogAndCount(operation, serviceName, message, logger);
                try
                {
                    await send().ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    ObvsDiagnostics.RecordFailure(activity, exception);
                    throw;
                }
            }
        }

        public static void LogAndCount(string operation, string serviceName, IMessage message, ILogger logger)
        {
            ObvsDiagnostics.RecordSent(operation, serviceName, message.GetType());
            logger.LogDebug("Obvs {Operation} {MessageType} on service '{ServiceName}'.", operation, message.GetType().Name, serviceName);
        }

        public static IObservable<T> CountReceived<T>(this IObservable<T> messages, string serviceName) where T : class
        {
            return messages.Do(message => ObvsDiagnostics.RecordReceived(serviceName, message.GetType()));
        }
    }
}
