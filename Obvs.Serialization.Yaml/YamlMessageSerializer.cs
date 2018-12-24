using System.IO;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.RepresentationModel;

namespace Obvs.Serialization.Yaml {
    
    /// <summary>
    /// YAML message serializer
    /// </summary>
    public class YamlMessageSerializer : IMessageSerializer {
        private readonly ISerializer _serializer;

        private static readonly ISerializer DEFAULT_SERIALIZER = new SerializerBuilder()
                                                                    .WithNamingConvention(new CamelCaseNamingConvention())
                                                                    .Build();

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="serializer">Implementation of the YAML serializer to use</param>
        public YamlMessageSerializer(ISerializer serializer = null) {
            _serializer = serializer ?? DEFAULT_SERIALIZER;
        }

        /// <inheritdoc />
        public virtual void Serialize(Stream stream, object message) {
            using (var streamWriter = new StreamWriter(stream)) {
                _serializer.Serialize(streamWriter, message);
            }
        }
    }
}