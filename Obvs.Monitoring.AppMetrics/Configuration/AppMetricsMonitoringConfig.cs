using System;
using System.Collections.Generic;
using App.Metrics;
using Obvs.Configuration;

namespace Obvs.Monitoring.AppMetrics.Configuration
{
    public interface ICanSpecifyAppMetricsTypes<TMessage, TCommand, TEvent, TRequest, TResponse> :
        ICanSpecifyAppMetricsInstancePrefix<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TMessage : class
            where TCommand : class, TMessage
            where TEvent : class, TMessage
            where TRequest : class, TMessage
            where TResponse : class, TMessage
    {
        ICanSpecifyAppMetricsTypes<TMessage, TCommand, TEvent, TRequest, TResponse> AddCounter<T>() where T : class, TMessage;
    }

    public interface ICanSpecifyAppMetricsInstancePrefix<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TMessage : class
            where TCommand : class, TMessage
            where TEvent : class, TMessage
            where TRequest : class, TMessage
            where TResponse : class, TMessage
    {
        ICanSpecifyAppMetrics<TMessage, TCommand, TEvent, TRequest, TResponse> PrefixInstanceWith(string instancePrefix);
    }
    public interface ICanSpecifyAppMetrics<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TMessage : class
            where TCommand : class, TMessage
            where TEvent : class, TMessage
            where TRequest : class, TMessage
            where TResponse : class, TMessage
    {
        ICanSpecifyLoggingOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse> WithMetrics(IMetrics metrics);
    }

    public class AppMetricsMonitoringConfig<TMessage, TCommand, TEvent, TRequest, TResponse> :
        ICanSpecifyAppMetricsTypes<TMessage, TCommand, TEvent, TRequest, TResponse>,
        ICanSpecifyAppMetrics<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TMessage : class
            where TCommand : class, TMessage
            where TEvent : class, TMessage
            where TRequest : class, TMessage
            where TResponse : class, TMessage
    {
        private readonly ICanAddEndpointOrLoggingOrCorrelationOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse> _config;
        private readonly List<Type> _types = new List<Type>();
        private string _prefix;

        public AppMetricsMonitoringConfig(ICanAddEndpointOrLoggingOrCorrelationOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse> config)
        {
            _config = config;
        }

        public ICanSpecifyLoggingOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse> WithMetrics(IMetrics metrics)
        {
            return _config.UsingMonitor(new AppMetricsMonitorFactory<TMessage>(metrics, _types, _prefix));
        }

        public ICanSpecifyAppMetrics<TMessage, TCommand, TEvent, TRequest, TResponse> PrefixInstanceWith(string instancePrefix)
        {
            _prefix = instancePrefix;
            return this;
        }

        public ICanSpecifyAppMetricsTypes<TMessage, TCommand, TEvent, TRequest, TResponse> AddCounter<T>() where T : class, TMessage
        {
            _types.Add(typeof (T));
            return this;
        }
    }
}