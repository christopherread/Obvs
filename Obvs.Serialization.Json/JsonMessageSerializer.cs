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

        public virtual object Serialize(object message)
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                Serialize(memoryStream, message);
                
                return memoryStream.ToArray();
            }
        }

        public virtual void Serialize(Stream stream, object message)
        {
            using (var streamWriter = new StreamWriter(stream, JsonMessageDefaults.Encoding, 1024, true))
            {
                _serializer.Serialize(streamWriter, message);
            }
        }
    }
}