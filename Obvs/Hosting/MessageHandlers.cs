using System.Threading;
using System.Threading.Tasks;
using Obvs.Types;

namespace Obvs.Hosting
{
    /// <summary>
    /// Handles a command. Register with <c>HandleCommand&lt;TCommand, THandler&gt;()</c>; a new instance is
    /// resolved from its own DI scope for each command, so it can depend on scoped services.
    /// </summary>
    public interface ICommandHandler<in TCommand> where TCommand : class, ICommand
    {
        /// <summary>Handles <paramref name="command"/>. Throwing fails it, and it's settled as its transport allows.</summary>
        /// <param name="command">The command</param>
        /// <param name="context">Publishes and sends messages as part of handling this one</param>
        /// <param name="cancellationToken">Cancelled when the host stops</param>
        Task HandleAsync(TCommand command, MessageContext context, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Handles an event. Register with <c>HandleEvent&lt;TEvent, THandler&gt;()</c>; a new instance is
    /// resolved from its own DI scope for each event.
    /// </summary>
    public interface IEventHandler<in TEvent> where TEvent : class, IEvent
    {
        /// <summary>Handles <paramref name="ev"/>. Throwing fails it, and it's settled as its transport allows.</summary>
        /// <param name="ev">The event</param>
        /// <param name="context">Publishes and sends messages as part of handling this one</param>
        /// <param name="cancellationToken">Cancelled when the host stops</param>
        Task HandleAsync(TEvent ev, MessageContext context, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Answers a request; the returned response is sent back to the requester. Register with
    /// <c>HandleRequest&lt;TRequest, TResponse, THandler&gt;()</c>; a new instance is resolved from its own DI scope for each request.
    /// </summary>
    public interface IRequestHandler<in TRequest, TResponse>
        where TRequest : class, IRequest
        where TResponse : class, IResponse
    {
        /// <summary>Answers <paramref name="request"/> with the response returned, which is sent back to the requester.</summary>
        /// <param name="request">The request</param>
        /// <param name="context">Publishes and sends messages as part of handling this one</param>
        /// <param name="cancellationToken">Cancelled when the host stops</param>
        Task<TResponse> HandleAsync(TRequest request, MessageContext context, CancellationToken cancellationToken);
    }
}
