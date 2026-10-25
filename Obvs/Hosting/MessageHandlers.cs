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
        Task HandleAsync(TCommand command, MessageContext context, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Handles an event. Register with <c>HandleEvent&lt;TEvent, THandler&gt;()</c>; a new instance is
    /// resolved from its own DI scope for each event.
    /// </summary>
    public interface IEventHandler<in TEvent> where TEvent : class, IEvent
    {
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
        Task<TResponse> HandleAsync(TRequest request, MessageContext context, CancellationToken cancellationToken);
    }
}
