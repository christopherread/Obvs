using System;
using App.Metrics;
using App.Metrics.Meter;
using App.Metrics.Timer;

namespace Obvs.Monitoring.AppMetrics
{
    public class AppMetricsMonitor<TMessage> : IMonitor<TMessage>
    {
        private readonly IMetrics _metrics;
        private static readonly MeterOptions CounterSent = new MeterOptions { Context = "Obvs", Name = "Messages Sent Count", MeasurementUnit = Unit.Custom("Messages") };
        private static readonly MeterOptions CounterReceived = new MeterOptions { Context = "Obvs", Name = "Messages Received Count", MeasurementUnit = Unit.Custom("Messages") };

        private static readonly TimerOptions CounterSentTime = new TimerOptions { Context = "Obvs", Name = "Messages Sent Elapsed", MeasurementUnit = Unit.Custom("Messages") };
        private static readonly TimerOptions CounterReceivedTime = new TimerOptions { Context = "Obvs", Name = "Messages Received Elapsed", MeasurementUnit = Unit.Custom("Messages") };

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
                    _metrics.Measure.Meter.Mark(CounterSent, _tags);
                    _metrics.Provider.Timer.Instance(CounterSentTime, _tags).Record((long) elapsed.TotalMilliseconds, TimeUnit.Milliseconds);
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
                    _metrics.Measure.Meter.Mark(CounterReceived, _tags);
                    _metrics.Provider.Timer.Instance(CounterReceivedTime, _tags).Record((long) elapsed.TotalMilliseconds, TimeUnit.Milliseconds);
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
