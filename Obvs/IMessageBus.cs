// v6's local in-memory bus, kept for v6's fluent configuration; v7 uses Obvs.Testing's in-memory transport: its public members are exempt from the rule that every one is documented
#pragma warning disable CS1570, CS1572, CS1591

namespace Obvs
{
    public interface IMessageBus<TMessage> : IMessagePublisher<TMessage>, IMessageSource<TMessage> 
        where TMessage : class 
    {
    }
}