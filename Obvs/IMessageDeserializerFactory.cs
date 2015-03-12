using System.Collections.Generic;
using Obvs.Types;

namespace Obvs
{
    public interface IMessageDeserializerFactory
    {
        IEnumerable<IMessageDeserializer<TMessage>> Create<TMessage, TServiceMessage>(string assemblyNameContains = null)
            where TMessage : IMessage
            where TServiceMessage : IMessage;
    }
}