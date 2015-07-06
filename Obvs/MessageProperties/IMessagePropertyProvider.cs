using System.Collections.Generic;
using System.Linq;

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