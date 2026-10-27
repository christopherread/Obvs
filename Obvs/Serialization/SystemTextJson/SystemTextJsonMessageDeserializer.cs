using System.IO;
using System.Text.Json;

namespace Obvs.Serialization.SystemTextJson
{
    public class SystemTextJsonMessageDeserializer<TMessage> : MessageDeserializerBase<TMessage>
        where TMessage : class
    {
        private readonly JsonSerializerOptions _options;

        /// <param name="options">Defaults to <see cref="SystemTextJsonMessageDefaults.Options"/></param>
        public SystemTextJsonMessageDeserializer(JsonSerializerOptions options = null)
        {
            _options = options ?? SystemTextJsonMessageDefaults.Options;
        }

        public override TMessage Deserialize(Stream stream)
        {
            return JsonSerializer.Deserialize<TMessage>(stream, _options);
        }
    }
}
