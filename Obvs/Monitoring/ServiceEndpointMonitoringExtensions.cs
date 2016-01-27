namespace Obvs.Monitoring
{
    public static class ServiceEndpointMonitoringExtensions
    {
        public static IServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse> CreateMonitoringProxy<TMessage, TCommand, TEvent, TRequest, TResponse>(this IServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse> endpoint,
            IMonitorFactory<TMessage> monitorFactory)
            where TMessage : class
            where TCommand : TMessage
            where TEvent : TMessage
            where TRequest : TMessage
            where TResponse : TMessage
        {
            return new ServiceEndpointClientMonitoringProxy<TMessage, TCommand, TEvent, TRequest, TResponse>(endpoint, monitorFactory);
        }

        public static IServiceEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse> CreateMonitoringProxy<TMessage, TCommand, TEvent, TRequest, TResponse>(
            this IServiceEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse> endpoint,
            IMonitorFactory<TMessage> monitorFactory)
            where TMessage : class
            where TCommand : TMessage
            where TEvent : TMessage
            where TRequest : TMessage
            where TResponse : TMessage
        {
            return new ServiceEndpointMonitoringProxy<TMessage, TCommand, TEvent, TRequest, TResponse>(endpoint, monitorFactory);
        }
    }
}