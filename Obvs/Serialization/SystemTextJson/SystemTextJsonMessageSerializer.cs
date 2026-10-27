using System.IO;
using System.Text.Json;

namespace Obvs.Serialization.SystemTextJson
{
    /// <summary>
    /// Serializes messages as JSON with System.Text.Json. A service uses it unless it calls SerializedWith.
    /// </summary>
    public class SystemTextJsonMessageSerializer : IMessageSerializer
    {
        private readonly JsonSerializerOptions _options;

        /// <param name="options">Defaults to <see cref="SystemTextJsonMessageDefaults.Options"/></param>
        public SystemTextJsonMessageSerializer(JsonSerializerOptions options = null)
        {
            _options = options ?? SystemTextJsonMessageDefaults.Options;
        }

        public void Serialize(Stream stream, object message)
        {
            JsonSerializer.Serialize(stream, message, message.GetType(), _options);
        }
    }
}
