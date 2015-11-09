using Obvs.Configuration;

namespace Obvs.Monitoring.PerformanceCounters.Configuration
{
    public static class ConfigExtensions
    {
        public static ICanSpecifyLoggingOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse> UsingPerfomanceCounterMonitoring<TMessage, TCommand, TEvent, TRequest, TResponse>(
          this ICanAddEndpointOrLoggingOrCorrelationOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse> config)
          where TMessage : class
          where TCommand : class, TMessage
          where TEvent : class, TMessage
          where TRequest : class, TMessage
          where TResponse : class, TMessage
        {
            return config.UsingMonitor(new PerformanceCounterMonitorFactory<TMessage>());
        }
    }
}