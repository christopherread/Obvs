namespace Obvs.Hosting
{
    /// <summary>
    /// The names Obvs traces and measures under. Add them to OpenTelemetry to collect them:
    /// <c>tracing.AddSource(ObvsTelemetry.ActivitySourceName)</c> and <c>metrics.AddMeter(ObvsTelemetry.MeterName)</c>.
    /// </summary>
    public static class ObvsTelemetry
    {
        /// <summary>
        /// The <c>ActivitySource</c> for spans: <c>send</c>, <c>publish</c>, <c>request</c> and <c>reply</c> for
        /// outgoing messages, and <c>process</c> for each handler run, following OpenTelemetry's messaging conventions.
        /// </summary>
        public const string ActivitySourceName = "Obvs";

        /// <summary>
        /// The <c>Meter</c> for metrics, each tagged with <c>messaging.system</c> (<c>obvs</c>), <c>messaging.operation.name</c>,
        /// <c>messaging.destination.name</c> (the service) and <c>obvs.message.type</c>:
        /// <list type="bullet">
        /// <item><description><c>messaging.client.sent.messages</c>: messages sent, published, requested or replied, with the operation's name.</description></item>
        /// <item><description><c>messaging.client.consumed.messages</c>: commands, events and requests received (operation <c>receive</c>), counted
        /// once each as they arrive, whether or not a handler takes them. Responses aren't counted, and nor are messages a transport
        /// couldn't deserialize.</description></item>
        /// <item><description><c>messaging.process.duration</c>: how long each handler run took, in seconds (operation <c>process</c>), with
        /// <c>error.type</c> (the exception's full type name) when the handler threw.</description></item>
        /// <item><description><c>obvs.process.failed.messages</c>: messages whose handler threw (operation <c>process</c>), with <c>error.type</c>.
        /// It counts the same runs as <c>messaging.process.duration</c>'s <c>error.type</c>, as a counter to alert on.</description></item>
        /// </list>
        /// </summary>
        public const string MeterName = "Obvs";
    }
}
