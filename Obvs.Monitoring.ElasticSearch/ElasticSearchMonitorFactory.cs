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
        private readonly string _instanceName;
        private readonly TimeSpan _samplePeriod;
        private readonly IScheduler _scheduler;
        private readonly IElasticClient _client;

        public ElasticSearchMonitorFactory(string indexPrefx, IList<Type> types, string instanceName, TimeSpan samplePeriod, IScheduler scheduler, IElasticClient client)
        {
            _indexPrefx = indexPrefx;
            _types = types;
            _instanceName = instanceName;
            _samplePeriod = samplePeriod;
            _scheduler = scheduler;
            _client = client;
        }
        
        public IMonitor<TMessage> Create(string name)
        {
            return new ElasticSearchMonitor<TMessage>(_instanceName, name, _indexPrefx, _types, _samplePeriod, _scheduler, _client);
        }
    }
}