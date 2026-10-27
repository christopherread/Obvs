using System.IO;
using Newtonsoft.Json;

namespace Obvs.Serialization.Json
{
    /// <summary>Writes messages as JSON with Newtonsoft.Json, as v6 apps do. Use it with <c>SerializedWith</c>.</summary>
    public class JsonMessageSerializer : IMessageSerializer
    {
        private readonly JsonSerializer _serializer;

        /// <summary>A serializer with these settings.</summary>
        /// <param name="serializerSettings">Newtonsoft's settings, or its defaults</param>
        public JsonMessageSerializer(JsonSerializerSettings serializerSettings = null)
        {
            _serializer = serializerSettings == null ? JsonSerializer.Create() : JsonSerializer.Create(serializerSettings);
        }

        /// <inheritdoc />
        public virtual void Serialize(Stream stream, object message)
        {
            using (StreamWriter sw = new StreamWriter(stream, JsonMessageDefaults.Encoding, 1024, true))
            {
                using (JsonTextWriter jtx = new JsonTextWriter(sw))
                {
                    jtx.ArrayPool = JsonArrayPool.Instance;

                    _serializer.Serialize(jtx, message);
                }
            }
        }
    }
}