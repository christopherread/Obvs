// v6's monitoring, kept for v6 apps and replaced in v7 by OpenTelemetry: its public members are exempt from the rule that every one is documented
#pragma warning disable CS1570, CS1572, CS1591

namespace Obvs.Monitoring
{
    public interface IMonitorFactory<in TMessage>
    {
        IMonitor<TMessage> Create(string name);
    }
}