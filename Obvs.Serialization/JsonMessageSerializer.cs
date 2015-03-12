using Newtonsoft.Json;

namespace Obvs.Serialization
{
    public class JsonMessageSerializer : IMessageSerializer
    {
        public object Serialize(object message)
        {
            return JsonConvert.SerializeObject(message);
        }
    }
}