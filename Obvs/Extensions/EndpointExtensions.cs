using System;

namespace Obvs.Extensions
{
    public static class EndpointExtensions
    {
        public static IObservable<TEvent> EventsWithErrorHandling<TMessage, TCommand, TEvent, TRequest, TResponse>(this IServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse> endpoint, IObserver<Exception> exceptions)
            where TMessage : class
            where TCommand : TMessage
            where TEvent : TMessage
            where TRequest : TMessage
            where TResponse : TMessage
        {
            return endpoint.Events.CatchAndHandle(exceptions, () => endpoint.Events, string.Format("Error receiving {0} from endpoint {1}", typeof(TEvent), endpoint.Name));
        }

        public static IObservable<TCommand> CommandsWithErrorHandling<TMessage, TCommand, TEvent, TRequest, TResponse>(this IServiceEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse> endpoint, IObserver<Exception> exceptions) 
            where TMessage : class 
            where TCommand : TMessage 
            where TEvent : TMessage 
            where TRequest : TMessage 
            where TResponse : TMessage
        {
            return endpoint.Commands.CatchAndHandle(exceptions, () => endpoint.Commands, string.Format("Error receiving {0} from endpoint {1}", typeof(TCommand), endpoint.Name));
        }

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