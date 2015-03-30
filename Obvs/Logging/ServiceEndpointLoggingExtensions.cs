namespace Obvs.Logging
{
    public static class ServiceEndpointLoggingExtensions
    {
        public static IServiceEndpointClient CreateLoggingProxy(this IServiceEndpointClient endpoint, ILoggerFactory loggerFactory)
        {
            return new ServiceEndpointClientLoggingProxy(loggerFactory, endpoint);
        }
        
        public static IServiceEndpoint CreateLoggingProxy(this IServiceEndpoint endpoint, ILoggerFactory loggerFactory)
        {
            return new ServiceEndpointLoggingProxy(loggerFactory, endpoint);
        }
    }
}