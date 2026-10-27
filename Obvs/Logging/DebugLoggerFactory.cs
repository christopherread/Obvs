// v6's logging, kept for v6 apps and replaced in v7 by Microsoft.Extensions.Logging: its public members are exempt from the rule that every one is documented
#pragma warning disable CS1570, CS1572, CS1591

namespace Obvs.Logging
{
    public class DebugLoggerFactory : ILoggerFactory
    {
        public ILogger Create(string name)
        {
            return new DebugLogger(name);
        }

        public ILogger Create<T>()
        {
            return Create(typeof (T).FullName);
        }
    }
}