using System;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Obvs.Types;

namespace Obvs.Hosting
{
    // Wraps a service's endpoints to complete each message no handler of the service will settle as it arrives,
    // so subscribers to the bus's streams see messages as they always have. Handlers settle the rest.
    internal sealed class SettlingServiceEndpoint : IServiceEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse>
    {
        private readonly IServiceEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse> _inner;

        public SettlingServiceEndpoint(IServiceEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse> inner, Func<Type, bool> isHandled, string serviceName, ILogger logger)
        {
            _inner = inner;
            Requests = inner.Requests.CompleteUnhandled(isHandled, serviceName, logger);
            Commands = inner.Commands.CompleteUnhandled(isHandled, serviceName, logger);
        }

        public IObservable<IRequest> Requests { get; }

        public IObservable<ICommand> Commands { get; }

        public Task PublishAsync(IEvent ev, CancellationToken cancellationToken = default) => _inner.PublishAsync(ev, cancellationToken);

        public Task ReplyAsync(IRequest request, IResponse response, CancellationToken cancellationToken = default) => _inner.ReplyAsync(request, response, cancellationToken);

        public bool CanHandle(IMessage message) => _inner.CanHandle(message);

        public string Name => _inner.Name;

        public void Dispose() => _inner.Dispose();
    }

    internal sealed class SettlingServiceEndpointClient : IServiceEndpointClient<IMessage, ICommand, IEvent, IRequest, IResponse>
    {
        private readonly IServiceEndpointClient<IMessage, ICommand, IEvent, IRequest, IResponse> _inner;
        private readonly string _serviceName;
        private readonly ILogger _logger;

        public SettlingServiceEndpointClient(IServiceEndpointClient<IMessage, ICommand, IEvent, IRequest, IResponse> inner, Func<Type, bool> isHandled, string serviceName, ILogger logger)
        {
            _inner = inner;
            _serviceName = serviceName;
            _logger = logger;
            Events = inner.Events.CompleteUnhandled(isHandled, serviceName, logger);
        }

        public IObservable<IEvent> Events { get; }

        public Task SendAsync(ICommand command, CancellationToken cancellationToken = default) => _inner.SendAsync(command, cancellationToken);

        // No handler takes responses
        public IObservable<IResponse> GetResponses(IRequest request) => _inner.GetResponses(request).CompleteUnhandled(_ => false, _serviceName, _logger);

        public bool CanHandle(IMessage message) => _inner.CanHandle(message);

        public string Name => _inner.Name;

        public void Dispose() => _inner.Dispose();
    }

    internal static class SettlingObservableExtensions
    {
        public static IObservable<T> CompleteUnhandled<T>(this IObservable<T> messages, Func<Type, bool> isHandled, string serviceName, ILogger logger)
            where T : class
        {
            return messages.Do(message =>
            {
                if (!isHandled(message.GetType()))
                {
                    _ = Settlement.CompleteAsync(message, serviceName, logger);
                }
            });
        }
    }
}
