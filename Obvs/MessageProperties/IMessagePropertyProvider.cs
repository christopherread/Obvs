using System.Collections.Generic;
using System.Linq;

// v6's message property providers, kept for v6's fluent configuration: its public members are exempt from the rule that every one is documented
#pragma warning disable CS1570, CS1572, CS1591

namespace Obvs.MessageProperties
{
    public interface IMessagePropertyProvider<in TMessage> where TMessage : class
    {
        IEnumerable<KeyValuePair<string, object>> GetProperties(TMessage message);
    }

    public class DefaultPropertyProvider<TMessage> : IMessagePropertyProvider<TMessage> where TMessage : class
    {
        public IEnumerable<KeyValuePair<string, object>> GetProperties(TMessage message)
        {
            return Enumerable.Empty<KeyValuePair<string, object>>();
        }
    }
}