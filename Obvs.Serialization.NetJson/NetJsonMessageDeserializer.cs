using System.IO;

namespace Obvs.Serialization.NetJson
{
    public class NetJsonMessageDeserializer<TMessage> : MessageDeserializerBase<TMessage>
        where TMessage : class
    {
        static NetJsonMessageDeserializer()
        {
            NetJsonDefaults.Set();
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