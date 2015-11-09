using Obvs.Configuration;

namespace Obvs.Monitoring.PerformanceCounters.Configuration
{
    public static class ConfigExtensions
    {
        public static ICanSpecifyPerformanceCountersType<TMessage, TCommand, TEvent, TRequest, TResponse> UsingPerformanceCounterMonitoring<TMessage, TCommand, TEvent, TRequest, TResponse>(
          this ICanAddEndpointOrLoggingOrCorrelationOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse> config)
          where TMessage : class
          where TCommand : class, TMessage
          where TEvent : class, TMessage
          where TRequest : class, TMessage
          where TResponse : class, TMessage
        {
            return new PerformanceCountersMonitoringConfig<TMessage, TCommand, TEvent, TRequest, TResponse>(config);
        }
    }
}