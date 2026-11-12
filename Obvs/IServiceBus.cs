using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Obvs.Types;

namespace Obvs
{
    /// <summary>
    /// The bus as a service's server and client: it receives commands and requests, and publishes events and replies, as
    /// well as everything a <see cref="IServiceBusClient"/> does. Resolve it from the container after <c>AddObvs</c>.
    /// </summary>
    public interface IServiceBus : IServiceBusClient
    {
        /// <summary>The requests the bus receives, for the services it's the server of. Reply to each with <see cref="ReplyAsync"/>.</summary>
        IObservable<IRequest> Requests { get; }

        /// <summary>The commands the bus receives, for the services it's the server of.</summary>
        IObservable<ICommand> Commands { get; }

        /// <summary>Publishes <paramref name="ev"/> to every client of its service.</summary>
        /// <param name="ev">The event</param>
        /// <param name="cancellationToken">Stops waiting for the broker</param>
        Task PublishAsync(IEvent ev, CancellationToken cancellationToken = default);

        /// <summary>Publishes <paramref name="ev"/> to every client of its service, with <paramref name="headers"/>.</summary>
        /// <param name="ev">The event</param>
        /// <param name="headers">Headers to send with it, which its handlers read from <c>MessageContext.Headers</c>. Obvs sets the trace-context headers itself</param>
        /// <param name="cancellationToken">Stops waiting for the broker</param>
        Task PublishAsync(IEvent ev, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default);

        /// <summary>Sends <paramref name="response"/> to whoever sent <paramref name="request"/>.</summary>
        /// <param name="request">The request being answered</param>
        /// <param name="response">The answer</param>
        /// <param name="cancellationToken">Stops waiting for the broker</param>
        Task ReplyAsync(IRequest request, IResponse response, CancellationToken cancellationToken = default);

        /// <summary>
        /// The bus's commands of type <typeparamref name="TCommand"/>, read as <see cref="IServiceBusClient.ReadEventsAsync{TEvent}"/>
        /// reads events. A command handler (<c>HandleCommand</c>) does more for you: it settles each command, and runs in a scope.
        /// </summary>
        /// <param name="cancellationToken">Stops reading</param>
        /// <param name="capacity">How many commands the reader can fall behind by</param>
        IAsyncEnumerable<TCommand> ReadCommandsAsync<TCommand>(CancellationToken cancellationToken = default, int capacity = 1024)
            where TCommand : ICommand;
    }
}
