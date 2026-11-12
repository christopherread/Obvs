using System.IO;
using Newtonsoft.Json;

namespace Obvs.Serialization.Json
{
    /// <summary>Reads a <typeparamref name="TMessage"/> as JSON, with Newtonsoft.Json.</summary>
    public class JsonMessageDeserializer<TMessage> : MessageDeserializerBase<TMessage>
        where TMessage : class
    {
        private readonly JsonSerializer _serializer;

        /// <summary>A deserializer with these settings.</summary>
        /// <param name="serializerSettings">Newtonsoft's settings, or its defaults</param>
        public JsonMessageDeserializer(JsonSerializerSettings serializerSettings = null)
        {
            _serializer = serializerSettings == null ? JsonSerializer.Create() : JsonSerializer.Create(serializerSettings);
        }

        /// <inheritdoc />
        public override TMessage Deserialize(Stream stream)
        {
            using (var streamReader = new StreamReader(stream, JsonMessageDefaults.Encoding, false, 1024, true))
            {
                return DeserializeCore(streamReader);
            }
        }


        /// <summary>Reads a <typeparamref name="TMessage"/> from <paramref name="textReader"/>.</summary>
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