using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace Obvs.Serialization.Json
{
    public class JsonMessageDeserializer<TMessage> : MessageDeserializerBase<TMessage>
        where TMessage : class
    {
        private readonly JsonSerializer _serializer;

        public JsonMessageDeserializer()
        {
            _serializer = new JsonSerializer();
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
            using (var streamReader = new StreamReader(stream, JsonMessageDefaults.Encoding, false, 1024, true))
            {
                return DeserializeCore(streamReader);
            }
        }


        protected TMessage DeserializeCore(TextReader textReader)
        {
            using (JsonTextReader jtr = new JsonTextReader(textReader))
            {
                jtr.ArrayPool = JsonArrayPool.Instance;

                return _serializer.Deserialize<TMessage>(jtr);
            }
        }
    }
}