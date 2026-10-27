// v6's ActiveMQ transport building blocks, public for v6's fluent configuration; v7 apps use UseActiveMq(...): its public members are exempt from the rule that every one is documented
#pragma warning disable CS1570, CS1572, CS1591

namespace Obvs.ActiveMQ
{
    public enum AcknowledgementMode
    {
        AutoAcknowledge,
        ClientAcknowledge
    }
}