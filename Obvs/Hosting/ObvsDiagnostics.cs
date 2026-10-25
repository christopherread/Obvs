using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Obvs.Hosting
{
    // Spans and metrics, named after OpenTelemetry's messaging conventions. The service is the destination,
    // as that's the name Obvs users work with; broker-level names stay inside the transports.
    internal static class ObvsDiagnostics
    {
        private static readonly string Version = typeof(ObvsDiagnostics).Assembly.GetName().Version?.ToString();

        public static readonly ActivitySource Source = new ActivitySource(ObvsTelemetry.ActivitySourceName, Version);

        private static readonly Meter Meter = new Meter(ObvsTelemetry.MeterName, Version);

        private static readonly Counter<long> SentMessages = Meter.CreateCounter<long>(
            "messaging.client.sent.messages", "{message}", "Messages sent, published, requested or replied.");

        private static readonly Histogram<double> ProcessDuration = Meter.CreateHistogram<double>(
            "messaging.process.duration", "s", "How long handlers took to process messages.");

        public const string System = "obvs";

        // operation is send, publish, request or reply
        public static Activity StartProducer(string operation, string serviceName, Type messageType)
        {
            var activity = Source.StartActivity($"{operation} {serviceName}", ActivityKind.Producer);
            if (activity != null)
            {
                activity.SetTag("messaging.system", System);
                // The conventions' type for every outgoing message; the name says which Obvs operation it was
                activity.SetTag("messaging.operation.type", "send");
                activity.SetTag("messaging.operation.name", operation);
                activity.SetTag("messaging.destination.name", serviceName);
                activity.SetTag("obvs.message.type", messageType.Name);
            }
            return activity;
        }

        public static Activity StartProcess(string serviceName, Type messageType)
        {
            var activity = Source.StartActivity($"process {serviceName}", ActivityKind.Consumer);
            if (activity != null)
            {
                activity.SetTag("messaging.system", System);
                activity.SetTag("messaging.operation.type", "process");
                activity.SetTag("messaging.operation.name", "process");
                activity.SetTag("messaging.destination.name", serviceName);
                activity.SetTag("obvs.message.type", messageType.Name);
            }
            return activity;
        }

        public static void RecordFailure(Activity activity, Exception exception)
        {
            if (activity == null)
            {
                return;
            }
            activity.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity.SetTag("error.type", exception.GetType().FullName);
            activity.AddException(exception);
        }

        public static void RecordSent(string operation, string serviceName, Type messageType)
        {
            SentMessages.Add(1,
                new KeyValuePair<string, object>("messaging.system", System),
                new KeyValuePair<string, object>("messaging.operation.name", operation),
                new KeyValuePair<string, object>("messaging.destination.name", serviceName),
                new KeyValuePair<string, object>("obvs.message.type", messageType.Name));
        }

        public static void RecordProcessed(string serviceName, Type messageType, long startTimestamp, Exception failure)
        {
            var seconds = (Stopwatch.GetTimestamp() - startTimestamp) / (double)Stopwatch.Frequency;
            var tags = new TagList
            {
                { "messaging.system", System },
                { "messaging.operation.name", "process" },
                { "messaging.destination.name", serviceName },
                { "obvs.message.type", messageType.Name },
            };
            if (failure != null)
            {
                tags.Add("error.type", failure.GetType().FullName);
            }
            ProcessDuration.Record(seconds, tags);
        }
    }
}
