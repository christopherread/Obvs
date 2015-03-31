using System.Collections.Generic;
using System.Linq;
using Obvs.Types;

namespace Obvs.MessageProperties
{
    public interface IMessagePropertyProvider<in TMessage> where TMessage : IMessage
    {
        IEnumerable<KeyValuePair<string, object>> GetProperties(TMessage message);
    }

    public class DefaultPropertyProvider<TMessage> : IMessagePropertyProvider<TMessage> where TMessage : IMessage
    {
        public IEnumerable<KeyValuePair<string, object>> GetProperties(TMessage message)
        {
            return Enumerable.Empty<KeyValuePair<string, object>>();
        }
    }
}