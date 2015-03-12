using Newtonsoft.Json;
using Obvs.Types;

namespace Obvs.Serialization
{
    public class JsonMessageDeserializer<TMessage> : DeserializerBase<TMessage>, IMessageDeserializer<TMessage>
        where TMessage : IMessage
    {
        public TMessage Deserialize(object obj)
        {
            return JsonConvert.DeserializeObject<TMessage>((string)obj);
        }
    }
}