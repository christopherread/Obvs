using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using Elasticsearch.Net;
using Nest;
using Obvs.Configuration;

namespace Obvs.Monitoring.ElasticSearch.Configuration
{
    public interface ICanSpecifyElasticSearchMonitoringType<TMessage, TCommand, TEvent, TRequest, TResponse> :
        ICanSpecifyElasticSearchMonitoringInstanceName<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TMessage : class
            where TCommand : class, TMessage
            where TEvent : class, TMessage
            where TRequest : class, TMessage
            where TResponse : class, TMessage
    {
        ICanSpecifyElasticSearchMonitoringType<TMessage, TCommand, TEvent, TRequest, TResponse> AddCounter<T>() where T : class, TMessage;
    }

    public interface ICanSpecifyElasticSearchMonitoringInstanceName<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TMessage : class
            where TCommand : class, TMessage
            where TEvent : class, TMessage
            where TRequest : class, TMessage
            where TResponse : class, TMessage
    {
        ICanSpecifyElasticSearchMonitoringSamplePeriod<TMessage, TCommand, TEvent, TRequest, TResponse> InstanceNamed(string instanceName);
    }

    public interface ICanSpecifyElasticSearchMonitoringSamplePeriod<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TMessage : class
            where TCommand : class, TMessage
            where TEvent : class, TMessage
            where TRequest : class, TMessage
            where TResponse : class, TMessage
    {
        ICanSpecifyElasticSearchMonitoringIndex<TMessage, TCommand, TEvent, TRequest, TResponse> SampleEvery(TimeSpan samplePeriod);
    }

    public interface ICanSpecifyElasticSearchMonitoringIndex<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TMessage : class
            where TCommand : class, TMessage
            where TEvent : class, TMessage
            where TRequest : class, TMessage
            where TResponse : class, TMessage
    {
        ICanSpecifyElasticSearchMonitoringUri<TMessage, TCommand, TEvent, TRequest, TResponse> SaveToIndex(string indexPrefix);
    }

    public interface ICanSpecifyElasticSearchMonitoringUri<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TMessage : class
        where TCommand : class, TMessage
        where TEvent : class, TMessage
        where TRequest : class, TMessage
        where TResponse : class, TMessage
    {
        ICanAddEndpointOrLoggingOrCorrelationOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse> ConnectToServer(string uri);

        ICanAddEndpointOrLoggingOrCorrelationOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse> ConnectToServer(IConnectionPool elasticConnectionPool);
    }

    internal class ElasticSearchMonitoringConfig<TMessage, TCommand, TEvent, TRequest, TResponse> :
        ICanSpecifyElasticSearchMonitoringType<TMessage, TCommand, TEvent, TRequest, TResponse>,
        ICanSpecifyElasticSearchMonitoringInstanceName<TMessage, TCommand, TEvent, TRequest, TResponse>,
        ICanSpecifyElasticSearchMonitoringSamplePeriod<TMessage, TCommand, TEvent, TRequest, TResponse>,
        ICanSpecifyElasticSearchMonitoringIndex<TMessage, TCommand, TEvent, TRequest, TResponse>,
        ICanSpecifyElasticSearchMonitoringUri<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TMessage : class
        where TCommand : class, TMessage
        where TEvent : class, TMessage
        where TRequest : class, TMessage
        where TResponse : class, TMessage
    {
        private readonly ICanAddEndpointOrLoggingOrCorrelationOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse> _config;
        private string _indexPrefix;
        private readonly List<Type> _types = new List<Type>();
        private string _instanceName;
        private TimeSpan _samplePeriod;

        public ElasticSearchMonitoringConfig(ICanAddEndpointOrLoggingOrCorrelationOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse> config)
        {
            _config = config;
        }

        public ICanSpecifyElasticSearchMonitoringUri<TMessage, TCommand, TEvent, TRequest, TResponse> SaveToIndex(string indexPrefix)
        {
            _indexPrefix = indexPrefix;
            return this;
        }

        public ICanAddEndpointOrLoggingOrCorrelationOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse> ConnectToServer(string uri)
        {
            var monitorFactory = new ElasticSearchMonitorFactory<TMessage>(
                _indexPrefix, _types, _instanceName,
                _samplePeriod, Scheduler.Default, 
                new ElasticClient(new ConnectionSettings(new Uri(uri))));

            _config.UsingMonitor(monitorFactory);
            return _config;
        }

        public ICanAddEndpointOrLoggingOrCorrelationOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse> ConnectToServer(IConnectionPool elasticConnectionPool)
        {
            var monitorFactory = new ElasticSearchMonitorFactory<TMessage>(
                _indexPrefix, _types, _instanceName,
                _samplePeriod, Scheduler.Default,
                new ElasticClient(new ConnectionSettings(elasticConnectionPool)));

            _config.UsingMonitor(monitorFactory);
            return _config;
        }

        public ICanSpecifyElasticSearchMonitoringType<TMessage, TCommand, TEvent, TRequest, TResponse> AddCounter<T>() where T : class, TMessage
        {
            _types.Add(typeof(T));
            return this;
        }

        public ICanSpecifyElasticSearchMonitoringSamplePeriod<TMessage, TCommand, TEvent, TRequest, TResponse> InstanceNamed(string instanceName)
        {
            _instanceName = instanceName;
            return this;
        }

        public ICanSpecifyElasticSearchMonitoringIndex<TMessage, TCommand, TEvent, TRequest, TResponse> SampleEvery(TimeSpan samplePeriod)
        {
            _samplePeriod = samplePeriod;
            return this;
        }
    }
}