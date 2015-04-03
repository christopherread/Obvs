using Obvs.Configuration;

namespace Obvs.Logging.NLog.Configuration
{
    public static class FluentConfigExtensions
    {
        public static ICanCreate UsingNLog(this ICanAddEndpointOrLoggingOrCreate configurator)
        {
            return configurator.UsingLogging(new NLogLoggerFactory());
        }
    }
}