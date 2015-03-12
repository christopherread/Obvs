using Newtonsoft.Json;

namespace Obvs.Serialization.Json
{
    public class JsonMessageSerializer : IMessageSerializer
    {
        public object Serialize(object message)
        {
            return JsonConvert.SerializeObject(message);
        }
    }
}