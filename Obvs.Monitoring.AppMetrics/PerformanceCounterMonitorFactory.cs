using System;
using System.Collections.Generic;
using App.Metrics;

namespace Obvs.Monitoring.AppMetrics
{
    public class AppMetricsMonitorFactory<TMessage> : IMonitorFactory<TMessage>
    {
        private readonly IMetrics _metrics;
        private readonly List<Type> _types;
        private readonly string _instancePrefix;

        public AppMetricsMonitorFactory(IMetrics metrics, List<Type> types, string instancePrefix)
        {
            _metrics = metrics;
            _types = types;
            _instancePrefix = instancePrefix;
        }

        public IMonitor<TMessage> Create(string name)
        {
            var instanceName = name;
            if (!string.IsNullOrEmpty(_instancePrefix))
                instanceName = string.Format("{0}-{1}", _instancePrefix, name);

            return new AppMetricsMonitor<TMessage>(instanceName, _metrics);
        }
    }
}