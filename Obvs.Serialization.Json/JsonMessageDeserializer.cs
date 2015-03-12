using Newtonsoft.Json;
using Obvs.Types;

namespace Obvs.Serialization.Json
{
    public class JsonMessageDeserializer<TMessage> : MessageDeserializerBase<TMessage>
        where TMessage : IMessage
    {
        public override TMessage Deserialize(object obj)
        {
            return JsonConvert.DeserializeObject<TMessage>((string)obj);
        }
    }
}