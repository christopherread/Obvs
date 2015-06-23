using System;

namespace Obvs.Logging
{
    public static class ServiceEndpointLoggingExtensions
    {
        public static IServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse> CreateLoggingProxy<TMessage, TCommand, TEvent, TRequest, TResponse>(this IServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse> endpoint, 
            ILoggerFactory loggerFactory, 
            Func<Type, LogLevel> logLevelSend, 
            Func<Type, LogLevel> logLevelReceive)
            where TMessage : class
            where TCommand : TMessage
            where TEvent : TMessage
            where TRequest : TMessage
            where TResponse : TMessage
        {
            return new ServiceEndpointClientLoggingProxy<TMessage, TCommand, TEvent, TRequest, TResponse>(loggerFactory, endpoint, logLevelSend, logLevelReceive);
        }

        public static IServiceEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse> CreateLoggingProxy<TMessage, TCommand, TEvent, TRequest, TResponse>(this IServiceEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse> endpoint, 
            ILoggerFactory loggerFactory, 
            Func<Type, LogLevel> logLevelSend, 
            Func<Type, LogLevel> logLevelReceive)
            where TMessage : class
            where TCommand : TMessage
            where TEvent : TMessage
            where TRequest : TMessage
            where TResponse : TMessage
        {
            return new ServiceEndpointLoggingProxy<TMessage, TCommand, TEvent, TRequest, TResponse>(loggerFactory, endpoint, logLevelSend, logLevelReceive);
        }
    }
}