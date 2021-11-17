using System.IO;
using Newtonsoft.Json;

namespace Obvs.Serialization.Json
{
    public class JsonMessageDeserializer<TMessage> : MessageDeserializerBase<TMessage>
        where TMessage : class
    {
        private readonly JsonSerializer _serializer;

        public JsonMessageDeserializer(JsonSerializerSettings serializerSettings = null)
        {
            _serializer = serializerSettings == null ? JsonSerializer.Create() : JsonSerializer.Create(serializerSettings);
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