using System.IO;
using Newtonsoft.Json;

namespace Obvs.Serialization.Json
{
    public class JsonMessageSerializer : IMessageSerializer
    {
        private readonly JsonSerializer _serializer;

        public JsonMessageSerializer()
        {
            _serializer = new JsonSerializer();
        }

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