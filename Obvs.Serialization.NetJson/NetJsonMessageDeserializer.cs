using System.IO;
using NetJSON;

namespace Obvs.Serialization.NetJson
{
    public class NetJsonMessageDeserializer<TMessage> : MessageDeserializerBase<TMessage>
        where TMessage : class
    {
        static NetJsonMessageDeserializer()
        {
            NetJsonDefaults.Set();
        }

        public override TMessage Deserialize(object obj)
        {
            return NetJSON.NetJSON.Deserialize<TMessage>((string)obj);
        }

        public override TMessage Deserialize(Stream stream)
        {
            return NetJSON.NetJSON.Deserialize<TMessage>(new StreamReader(stream));
        }
    }
}