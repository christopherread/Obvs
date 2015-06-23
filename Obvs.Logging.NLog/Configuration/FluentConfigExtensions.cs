using System;
using Obvs.Configuration;

namespace Obvs.Logging.NLog.Configuration
{
    public static class FluentConfigExtensions
    {
        public static ICanCreate<TMessage, TCommand, TEvent, TRequest, TResponse> UsingNLog<TMessage, TCommand, TEvent, TRequest, TResponse>(this ICanAddEndpointOrLoggingOrCorrelationOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse> configurator, 
            Func<IEndpoint<TMessage>, bool> enableLogging = null, 
            Func<Type, LogLevel> logLevelSend = null, 
            Func<Type, LogLevel> logLevelReceive = null) 
            where TMessage : class 
            where TCommand : class, TMessage 
            where TEvent : class, TMessage 
            where TRequest : class, TMessage 
            where TResponse : class, TMessage
        {
            return configurator.UsingLogging(new NLogLoggerFactory(), enableLogging, logLevelSend, logLevelReceive);
        }
    }
}