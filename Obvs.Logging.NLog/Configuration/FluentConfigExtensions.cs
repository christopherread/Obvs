using System;
using Obvs.Configuration;

namespace Obvs.Logging.NLog.Configuration
{
    public static class FluentConfigExtensions
    {
        public static ICanCreate UsingNLog(this ICanAddEndpointOrLoggingOrCorrelationOrCreate configurator, Func<IEndpoint, bool> enableLogging = null)
        {
            return configurator.UsingLogging(new NLogLoggerFactory(), enableLogging);
        }
    }
}