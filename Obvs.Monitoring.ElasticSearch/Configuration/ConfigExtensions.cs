using Obvs.Configuration;

namespace Obvs.Monitoring.ElasticSearch.Configuration
{
    public static class ConfigExtensions
    {
        public static ICanSpecifyElasticSearchMonitoringType<TMessage, TCommand, TEvent, TRequest, TResponse> UsingElasticSearchMonitoring<TMessage, TCommand, TEvent, TRequest, TResponse>(
            this ICanAddEndpointOrLoggingOrCorrelationOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse> config) 
            where TMessage : class 
            where TCommand : class, TMessage 
            where TEvent : class, TMessage 
            where TRequest : class, TMessage 
            where TResponse : class, TMessage
        {
            return new ElasticSearchMonitoringConfig<TMessage, TCommand, TEvent, TRequest, TResponse>(config);
        }
    }
}