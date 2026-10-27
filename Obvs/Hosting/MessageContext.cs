using System;
using System.Threading;
using System.Threading.Tasks;
using Obvs.Types;

namespace Obvs.Hosting
{
    /// <summary>
    /// What a handler needs besides the message: the service it arrived on, the services of its DI scope,
    /// and the bus, to publish or send further messages.
    /// </summary>
    public sealed class MessageContext
    {
        private readonly IServiceBus _bus;

        internal MessageContext(string serviceName, IServiceProvider services, IServiceBus bus)
        {
            ServiceName = serviceName;
            Services = services;
            _bus = bus;
        }

        /// <summary>The name of the service the message arrived on.</summary>
        public string ServiceName { get; }

        /// <summary>The services of this message's DI scope, disposed when the handler finishes.</summary>
        public IServiceProvider Services { get; }

        /// <summary>Publishes an event.</summary>
        public Task PublishAsync(IEvent ev, CancellationToken cancellationToken = default)
        {
            return _bus.PublishAsync(ev, cancellationToken);
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
    }
}
