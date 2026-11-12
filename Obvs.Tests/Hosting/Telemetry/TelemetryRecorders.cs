using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using Obvs.Hosting;

namespace Obvs.Tests.Hosting.Telemetry
{
    // Listeners are process-wide and tests run in parallel, so each test reads only its own service's telemetry
    public sealed class ActivityRecorder : IDisposable
    {
        private readonly ConcurrentQueue<Activity> _stopped = new ConcurrentQueue<Activity>();
        private readonly ActivityListener _listener;

        public ActivityRecorder()
        {
            _listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == ObvsTelemetry.ActivitySourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = activity => _stopped.Enqueue(activity),
            };
            ActivitySource.AddActivityListener(_listener);
        }

        public IReadOnlyList<Activity> For(string serviceName)
        {
            return _stopped.Where(activity => (activity.GetTagItem("messaging.destination.name") as string) == serviceName).ToList();
        }

        public void Dispose() => _listener.Dispose();
    }

    public sealed record Measurement(string Instrument, double Value, IReadOnlyDictionary<string, object> Tags);

    public sealed class MetricRecorder : IDisposable
    {
        private readonly ConcurrentQueue<Measurement> _measurements = new ConcurrentQueue<Measurement>();
        private readonly MeterListener _listener = new MeterListener();

        public MetricRecorder()
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == ObvsTelemetry.MeterName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => Record(instrument, value, tags));
            _listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) => Record(instrument, value, tags));
            _listener.Start();
        }

        public IReadOnlyList<Measurement> For(string serviceName, string instrument)
        {
            return _measurements
                .Where(measurement => measurement.Instrument == instrument &&
                                      measurement.Tags.TryGetValue("messaging.destination.name", out var service) && (service as string) == serviceName)
                .ToList();
        }

        public void Dispose() => _listener.Dispose();

        private void Record(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object>> tags)
        {
            var tagDictionary = new Dictionary<string, object>();
            foreach (var tag in tags)
            {
                tagDictionary[tag.Key] = tag.Value;
            }
            _measurements.Enqueue(new Measurement(instrument.Name, value, tagDictionary));
        }
    }
}
