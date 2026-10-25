using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Obvs.Types;

namespace Obvs.Hosting
{
    // Wraps a service's endpoints, which know which service a message belongs to, to trace, count and
    // log (at Debug) every outgoing message
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
        }

        public IObservable<IRequest> Requests => _inner.Requests;

        public IObservable<ICommand> Commands => _inner.Commands;

        public Task PublishAsync(IEvent ev)
        {
            return Telemetry.SendAsync("publish", _serviceName, ev, _logger, () => _inner.PublishAsync(ev));
        }

        public Task ReplyAsync(IRequest request, IResponse response)
        {
            return Telemetry.SendAsync("reply", _serviceName, response, _logger, () => _inner.ReplyAsync(request, response));
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
        }

        public IObservable<IEvent> Events => _inner.Events;

        public Task SendAsync(ICommand command)
        {
            return Telemetry.SendAsync("send", _serviceName, command, _logger, () => _inner.SendAsync(command));
        }

        // The inner endpoint sends the request when subscribed to, so the span covers the subscription
        public IObservable<IResponse> GetResponses(IRequest request)
        {
            return Observable.Create<IResponse>(observer =>
            {
                using (var activity = ObvsDiagnostics.StartProducer("request", _serviceName, request.GetType()))
                {
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
    }
}
