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
            return _serializer.Deserialize<TMessage>(new JsonTextReader(new StringReader((string)obj)));
        }

        public override TMessage Deserialize(Stream stream)
        {
            return _serializer.Deserialize<TMessage>(new JsonTextReader(new StreamReader(stream)));
        }
    }
}