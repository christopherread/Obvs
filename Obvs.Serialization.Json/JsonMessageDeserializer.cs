using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace Obvs.Serialization.Json
{
    public class JsonMessageDeserializer<TMessage> : MessageDeserializerBase<TMessage>
        where TMessage : class
    {
        private readonly JsonSerializer _serializer;
        private static readonly Encoding Encoding = new UTF8Encoding(false);

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
            using (var jsonTextReader = new JsonTextReader(textReader))
            {
                return _serializer.Deserialize<TMessage>(jsonTextReader);
            }
        }
    }
}