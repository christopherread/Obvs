using System.IO;
using NetJSON;

namespace Obvs.Serialization.NetJson
{
    public class NetJsonMessageSerializer : IMessageSerializer
    {
        static NetJsonMessageSerializer()
        {
            NetJsonDefaults.Set();
        }

        public object Serialize(object message)
        {
            return NetJSON.NetJSON.Serialize(message.GetType(), message);
        }

        public void Serialize(Stream stream, object message)
        {
            using (TextWriter writer = new StreamWriter(stream) { AutoFlush = true })
            {
                writer.Write(Serialize(message));
            }
        }
    }
}