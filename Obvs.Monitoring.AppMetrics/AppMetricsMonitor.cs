using System;
using App.Metrics;
using App.Metrics.Timer;

namespace Obvs.Monitoring.AppMetrics
{
    public class AppMetricsMonitor<TMessage> : IMonitor<TMessage>
    {
        private readonly IMetrics _metrics;

        private static readonly TimerOptions CounterSent = new TimerOptions { Context = "Obvs", Name = "Messages Sent", MeasurementUnit = Unit.Custom("Messages") };
        private static readonly TimerOptions CounterReceived = new TimerOptions { Context = "Obvs", Name = "Messages Received", MeasurementUnit = Unit.Custom("Messages") };

        private readonly MetricTags _tags;

        private bool _enabled = true;

        public AppMetricsMonitor(string instanceName, IMetrics metrics)
        {
            try
            {
                _tags = new MetricTags("i", instanceName);
                _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
            }
            catch (Exception exception)
            {
                _enabled = false;
                Console.WriteLine(exception);
            }
        }

        public void Dispose()
        {
        }

        public void MessageSent(TMessage message, TimeSpan elapsed)
        {
            if (_enabled)
            {
                try
                {
                    _metrics.Provider.Timer.Instance(CounterSent, _tags).Record((long) elapsed.TotalMilliseconds, TimeUnit.Milliseconds);
                }
                catch (Exception exception)
                {
                    _enabled = false;
                    Console.WriteLine(exception);
                }
            }
        }

        public void MessageReceived(TMessage message, TimeSpan elapsed)
        {
            if (_enabled)
            {
                try
                {
                    _metrics.Provider.Timer.Instance(CounterReceived, _tags).Record((long) elapsed.TotalMilliseconds, TimeUnit.Milliseconds);
                }
                catch (Exception exception)
                {
                    _enabled = false;
                    Console.WriteLine(exception);
                }
            }
        }
    }
}
