using System;
using Obvs.Configuration;

namespace Obvs.Logging.Log4Net.Configuration
{
    public static class FluentConfigExtensions
    {
        public static ICanCreate UsingLog4Net(this ICanAddEndpointOrLoggingOrCreate configurator, Func<IEndpoint, bool> enableLogging = null)
        {
            return configurator.UsingLogging(new Log4NetLogFactory(), enableLogging);
        }
    }
}