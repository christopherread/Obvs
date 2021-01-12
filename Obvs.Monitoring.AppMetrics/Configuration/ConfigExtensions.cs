using Obvs.Configuration;

namespace Obvs.Monitoring.AppMetrics.Configuration
{
    public static class ConfigExtensions
    {
        public static ICanSpecifyAppMetricsTypes<TMessage, TCommand, TEvent, TRequest, TResponse> UsingAppMetricsMonitoring<TMessage, TCommand, TEvent, TRequest, TResponse>(
          this ICanAddEndpointOrLoggingOrCorrelationOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse> config)
          where TMessage : class
          where TCommand : class, TMessage
          where TEvent : class, TMessage
          where TRequest : class, TMessage
          where TResponse : class, TMessage
        {
            return new AppMetricsMonitoringConfig<TMessage, TCommand, TEvent, TRequest, TResponse>(config);
        }
    }
}