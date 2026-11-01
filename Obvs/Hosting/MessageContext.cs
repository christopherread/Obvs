using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Obvs.Types;

namespace Obvs.Hosting
{
    /// <summary>
    /// What a handler needs besides the message: the service it arrived on, the headers it arrived with, the
    /// services of its DI scope, and the bus, to publish, send or request further messages.
    /// </summary>
    public sealed class MessageContext
    {
        private readonly IServiceBus _bus;

        internal MessageContext(string serviceName, IReadOnlyDictionary<string, string> headers, IServiceProvider services, IServiceBus bus)
        {
            ServiceName = serviceName;
            Headers = headers;
            Services = services;
            _bus = bus;
        }

        /// <summary>The name of the service the message arrived on.</summary>
        public string ServiceName { get; }

        /// <summary>
        /// The headers the message arrived with: the ones its sender passed, and its trace context. Pass them to
        /// <see cref="PublishAsync(IEvent, IReadOnlyDictionary{string, string}, CancellationToken)"/> or
        /// <see cref="SendAsync(ICommand, IReadOnlyDictionary{string, string}, CancellationToken)"/> to send them on.
        /// </summary>
        public IReadOnlyDictionary<string, string> Headers { get; }

        /// <summary>The services of this message's DI scope, disposed when the handler finishes.</summary>
        public IServiceProvider Services { get; }

        /// <summary>Publishes an event.</summary>
        public Task PublishAsync(IEvent ev, CancellationToken cancellationToken = default)
        {
            return _bus.PublishAsync(ev, cancellationToken);
        }

        /// <summary>Publishes an event with <paramref name="headers"/>. Obvs sets the trace-context headers itself.</summary>
        public Task PublishAsync(IEvent ev, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default)
        {
            return _bus.PublishAsync(ev, headers, cancellationToken);
        }

        internal Task ReplyAsync(IRequest request, IResponse response, CancellationToken cancellationToken)
        {
            return _bus.ReplyAsync(request, response, cancellationToken);
        }

        /// <summary>Sends a command.</summary>
        public Task SendAsync(ICommand command, CancellationToken cancellationToken = default)
        {
            return _bus.SendAsync(command, cancellationToken);
        }

        /// <summary>Sends a command with <paramref name="headers"/>. Obvs sets the trace-context headers itself.</summary>
        public Task SendAsync(ICommand command, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default)
        {
            return _bus.SendAsync(command, headers, cancellationToken);
        }

        /// <summary>
        /// Sends a request and returns its response, the one the request declares with <see cref="IRequest{TResponse}"/>.
        /// Throws <see cref="TimeoutException"/> if none arrives within the bus's request timeout: 30 seconds, unless set
        /// with <c>UseRequestTimeout</c>.
        /// </summary>
        public Task<TResponse> RequestAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
            where TResponse : class, IResponse
        {
            return _bus.RequestAsync(request, cancellationToken);
        }

        /// <summary>
        /// Sends a request and returns its response, the one the request declares with <see cref="IRequest{TResponse}"/>.
        /// Throws <see cref="TimeoutException"/> if none arrives within <paramref name="timeout"/>.
        /// </summary>
        public Task<TResponse> RequestAsync<TResponse>(IRequest<TResponse> request, TimeSpan timeout, CancellationToken cancellationToken = default)
            where TResponse : class, IResponse
        {
            return _bus.RequestAsync(request, timeout, cancellationToken);
        }
    }
}
