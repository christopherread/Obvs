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
            using (TextWriter writer = new StringWriter())
            {
                _serializer.Serialize(writer, message);
                return writer.ToString();
            }
        }

        public void Serialize(Stream stream, object message)
        {
            _serializer.Serialize(new StreamWriter(stream) {AutoFlush = true}, message);
        }
    }
}