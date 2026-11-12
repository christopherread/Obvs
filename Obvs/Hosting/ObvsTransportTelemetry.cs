using System;

namespace Obvs.Hosting
{
    /// <summary>For transports to record, in Obvs's metrics, what only they see.</summary>
    public static class ObvsTransportTelemetry
    {
        /// <summary>
        /// Counts a message of <paramref name="messageTypeName"/> that arrived for <paramref name="serviceName"/> but
        /// couldn't be deserialized, as received with <c>error.type</c>. A message of a type this app doesn't know isn't
        /// one: services share queues and topics, so transports skip those.
        /// </summary>
        public static void RecordUndeserializable(string serviceName, string messageTypeName, Exception exception)
        {
            ObvsDiagnostics.RecordReceived(serviceName, messageTypeName, exception);
        }
    }
}
