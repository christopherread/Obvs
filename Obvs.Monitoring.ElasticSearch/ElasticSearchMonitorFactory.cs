using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using Nest;

namespace Obvs.Monitoring.ElasticSearch
{
    public class ElasticSearchMonitorFactory<TMessage> : IMonitorFactory<TMessage>
    {
        private readonly string _indexPrefx;
        private readonly IList<Type> _types;
        private readonly string _instancePrefix;
        private readonly IScheduler _scheduler;
        private readonly ConnectionSettings _connectionSettings;

        public ElasticSearchMonitorFactory(string elasticSearchUri, string indexPrefx, IList<Type> types, string instancePrefix, IScheduler scheduler)
        {
            _indexPrefx = indexPrefx;
            _types = types;
            _instancePrefix = instancePrefix;
            _scheduler = scheduler;
            _connectionSettings = new ConnectionSettings(new Uri(elasticSearchUri));
        }

        public IMonitor<TMessage> Create(string name)
        {
            return new ElasticSearchMonitor<TMessage>(string.Format("{0} - {1}", _instancePrefix, name), _connectionSettings, _indexPrefx, _types, _scheduler);
        }
    }
}