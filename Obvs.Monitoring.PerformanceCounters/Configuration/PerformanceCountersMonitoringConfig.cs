using System;
using System.Collections.Generic;
using Obvs.Configuration;

namespace Obvs.Monitoring.PerformanceCounters.Configuration
{
    public interface ICanSpecifyPerformanceCountersType<TMessage, TCommand, TEvent, TRequest, TResponse> :
        ICanSpecifyPerformanceCountersInstancePrefix<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TMessage : class
            where TCommand : class, TMessage
            where TEvent : class, TMessage
            where TRequest : class, TMessage
            where TResponse : class, TMessage
    {
        ICanSpecifyPerformanceCountersType<TMessage, TCommand, TEvent, TRequest, TResponse> AddCounter<T>() where T : class, TMessage;
    }

    public interface ICanSpecifyPerformanceCountersInstancePrefix<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TMessage : class
            where TCommand : class, TMessage
            where TEvent : class, TMessage
            where TRequest : class, TMessage
            where TResponse : class, TMessage
    {
        ICanSpecifyLoggingOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse> PrefixInstanceWith(string instancePrefix);
    }

    public class PerformanceCountersMonitoringConfig<TMessage, TCommand, TEvent, TRequest, TResponse> :
        ICanSpecifyPerformanceCountersType<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TMessage : class
            where TCommand : class, TMessage
            where TEvent : class, TMessage
            where TRequest : class, TMessage
            where TResponse : class, TMessage
    {
        private readonly ICanAddEndpointOrLoggingOrCorrelationOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse> _config;
        private readonly List<Type> _types = new List<Type>();

        public PerformanceCountersMonitoringConfig(ICanAddEndpointOrLoggingOrCorrelationOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse> config)
        {
            _config = config;
        }

        public ICanSpecifyLoggingOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse> PrefixInstanceWith(string instancePrefix)
        {
            return _config.UsingMonitor(new PerformanceCounterMonitorFactory<TMessage>(_types, instancePrefix));
        }

        public ICanSpecifyPerformanceCountersType<TMessage, TCommand, TEvent, TRequest, TResponse> AddCounter<T>() where T : class, TMessage
        {
            _types.Add(typeof (T));
            return this;
        }
    }
}