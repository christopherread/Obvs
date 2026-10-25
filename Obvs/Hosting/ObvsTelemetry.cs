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
        /// The <c>Meter</c> for metrics: <c>messaging.client.sent.messages</c> and <c>messaging.process.duration</c>.
        /// </summary>
        public const string MeterName = "Obvs";
    }
}
