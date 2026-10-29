using System;
using System.Collections.Generic;

// v6's message property providers, which the transports use: its public members are exempt from the rule that every one is documented
#pragma warning disable CS1570, CS1572, CS1591

namespace Obvs.MessageProperties
{
    public class FuncMessagePropertyProvider<TMessage> : IMessagePropertyProvider<TMessage> where TMessage : class
    {
        Func<TMessage, IEnumerable<KeyValuePair<string, object>>> _providerFunc;

        public FuncMessagePropertyProvider(Func<TMessage, KeyValuePair<string, object>> singlePropertyProviderFunc)
            : this(m => new[] { singlePropertyProviderFunc(m) })
        {
        }

        public FuncMessagePropertyProvider(Func<TMessage, IEnumerable<KeyValuePair<string, object>>> multiPropertyProviderFunc)
        {
            _providerFunc = multiPropertyProviderFunc;
        }

        public IEnumerable<KeyValuePair<string, object>> GetProperties(TMessage message)
        {
            return _providerFunc(message);
        }

        public static implicit operator FuncMessagePropertyProvider<TMessage>(Func<TMessage, IEnumerable<KeyValuePair<string, object>>> multiPropertyProviderFunc)
        {
            return new FuncMessagePropertyProvider<TMessage>(multiPropertyProviderFunc);
        }

        public static implicit operator FuncMessagePropertyProvider<TMessage>(Func<TMessage, KeyValuePair<string, object>> singleValuePropertyProviderFunc)
        {
            return new FuncMessagePropertyProvider<TMessage>(singleValuePropertyProviderFunc);
        }
    }    
}
