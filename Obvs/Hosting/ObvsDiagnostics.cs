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

        private static readonly Counter<long> ReceivedMessages = Meter.CreateCounter<long>(
            "messaging.client.consumed.messages", "{message}", "Commands, events and requests received, whether or not a handler takes them.");

        // The conventions have no failed-messages counter, as process.duration's error.type covers it, so this one is Obvs's own
        private static readonly Counter<long> FailedMessages = Meter.CreateCounter<long>(
            "obvs.process.failed.messages", "{message}", "Messages whose handler threw.");

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

        // A child of the sender's span when the message carries trace context, so the trace continues
        public static Activity StartProcess(string serviceName, Type messageType, ActivityContext parent)
        {
            var activity = Source.StartActivity($"process {serviceName}", ActivityKind.Consumer, parent);
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

        // Adds the span's trace context to the message's headers, keeping any the app set. Falls back to the
        // ambient span (an incoming web request, say) when nobody listens to Obvs's own spans.
        public static void InjectTraceContext(Activity activity, object message)
        {
            activity = activity ?? Activity.Current;
            if (activity == null)
            {
                return;
            }

            var headers = new Dictionary<string, string>();
            foreach (var header in ObvsMessageHeaders.Get(message))
            {
                headers[header.Key] = header.Value;
            }
            DistributedContextPropagator.Current.Inject(activity, headers,
                (carrier, name, value) => ((Dictionary<string, string>)carrier)[name] = value);
            ObvsMessageHeaders.Set(message, headers);
        }

        // The sender's span from the message's headers, if it has trace context
        public static ActivityContext ExtractParent(object message)
        {
            var headers = ObvsMessageHeaders.Get(message);
            DistributedContextPropagator.Current.ExtractTraceIdAndState(headers,
                (object carrier, string name, out string value, out IEnumerable<string> values) =>
                {
                    values = null;
                    ((IReadOnlyDictionary<string, string>)carrier).TryGetValue(name, out value);
                },
                out var traceParent, out var traceState);

            return ActivityContext.TryParse(traceParent, traceState, out var parent) ? parent : default(ActivityContext);
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

        public static void RecordSent(string operation, string serviceName, Type messageType, Exception failure = null)
        {
            SentMessages.Add(1, Tags(operation, serviceName, messageType.Name, failure));
        }

        public static void RecordReceived(string serviceName, Type messageType)
        {
            ReceivedMessages.Add(1, Tags("receive", serviceName, messageType.Name, null));
        }

        public static void RecordReceived(string serviceName, string messageTypeName, Exception failure)
        {
            ReceivedMessages.Add(1, Tags("receive", serviceName, messageTypeName, failure));
        }

        private static TagList Tags(string operation, string serviceName, string messageTypeName, Exception failure)
        {
            var tags = new TagList
            {
                { "messaging.system", System },
                { "messaging.operation.name", operation },
                { "messaging.destination.name", serviceName },
                { "obvs.message.type", messageTypeName },
            };
            if (failure != null)
            {
                tags.Add("error.type", failure.GetType().FullName);
            }
            return tags;
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
                FailedMessages.Add(1, tags);
            }
            ProcessDuration.Record(seconds, tags);
        }
    }
}
