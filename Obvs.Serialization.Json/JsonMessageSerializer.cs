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

        public object Serialize(object message)
        {
            using (MemoryStream stream = new MemoryStream(256))
            {
                Serialize(stream, message);
                using (StreamReader streamReader = new StreamReader(stream))
                {
                    stream.Position = 0;
                    return streamReader.ReadToEnd();
                }
            }
        }

        public void Serialize(Stream stream, object message)
        {
            _serializer.Serialize(new StreamWriter(stream) {AutoFlush = true}, message);
        }
    }
}