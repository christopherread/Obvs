using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace Obvs.Serialization.Json
{
    public class JsonMessageDeserializer<TMessage> : MessageDeserializerBase<TMessage>
    {
        private readonly JsonSerializer _serializer;

        public JsonMessageDeserializer()
        {
            _serializer = new JsonSerializer();
        }

        public override TMessage Deserialize(object obj)
        {
            using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes((string) obj)))
            {
                return Deserialize(stream);
            }
        }

        public override TMessage Deserialize(Stream stream)
        {
            return _serializer.Deserialize<TMessage>(new JsonTextReader(new StreamReader(stream)));
        }
    }
}