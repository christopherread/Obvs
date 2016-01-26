using System.IO;
using System.Text;
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
            using (var memoryStream = new MemoryStream((byte[])obj))
            {
                return Deserialize(memoryStream);
            }
        }

        public override TMessage Deserialize(Stream stream)
        {
            return DeserializeCore(stream);
        }

        protected TMessage DeserializeCore(Stream stream)
        {
            using (var streamReader = new StreamReader(stream, NetJsonDefaults.Encoding, false, 1024, true))
            {
                return NetJSON.NetJSON.Deserialize<TMessage>(streamReader);
            }
        }
    }
}