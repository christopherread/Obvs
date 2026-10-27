using System;

namespace Obvs.Extensions
{
    /// <summary>Endpoint streams that report errors to the bus's <c>Exceptions</c> stream and carry on.</summary>
    public static class EndpointExtensions
    {
        /// <summary>The client's events, with errors reported to <paramref name="exceptions"/> rather than ending the stream.</summary>
        /// <param name="endpoint">The service's client endpoint</param>
        /// <param name="exceptions">Where errors go</param>
        public static IObservable<TEvent> EventsWithErrorHandling<TMessage, TCommand, TEvent, TRequest, TResponse>(this IServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse> endpoint, IObserver<Exception> exceptions)
            where TMessage : class
            where TCommand : TMessage
            where TEvent : TMessage
            where TRequest : TMessage
            where TResponse : TMessage
        {
            return endpoint.Events.CatchAndHandle(exceptions, () => endpoint.Events, string.Format("Error receiving {0} from endpoint {1}", typeof(TEvent), endpoint.Name));
        }

        /// <summary>The server's commands, with errors reported to <paramref name="exceptions"/> rather than ending the stream.</summary>
        /// <param name="endpoint">The service's server endpoint</param>
        /// <param name="exceptions">Where errors go</param>
        public static IObservable<TCommand> CommandsWithErrorHandling<TMessage, TCommand, TEvent, TRequest, TResponse>(this IServiceEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse> endpoint, IObserver<Exception> exceptions) 
            where TMessage : class 
            where TCommand : TMessage 
            where TEvent : TMessage 
            where TRequest : TMessage 
            where TResponse : TMessage
        {
            return endpoint.Commands.CatchAndHandle(exceptions, () => endpoint.Commands, string.Format("Error receiving {0} from endpoint {1}", typeof(TCommand), endpoint.Name));
        }

        /// <summary>The server's requests, with errors reported to <paramref name="exceptions"/> rather than ending the stream.</summary>
        /// <param name="endpoint">The service's server endpoint</param>
        /// <param name="exceptions">Where errors go</param>
        public static IObservable<TRequest> RequestsWithErrorHandling<TMessage, TCommand, TEvent, TRequest, TResponse>(this IServiceEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse> endpoint, IObserver<Exception> exceptions)
            where TMessage : class
            where TCommand : TMessage
            where TEvent : TMessage
            where TRequest : TMessage
            where TResponse : TMessage
        {
            return endpoint.Requests.CatchAndHandle(exceptions, () => endpoint.Requests, string.Format("Error receiving {0} from endpoint {1}", typeof(TRequest), endpoint.Name));
        }
    }
}