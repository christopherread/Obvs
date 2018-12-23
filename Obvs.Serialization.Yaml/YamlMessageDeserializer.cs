using System;
using System.IO;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.RepresentationModel;

namespace Obvs.Serialization.Yaml {

    /// <summary>
    /// YAML message deserializer
    /// </summary>
    /// <typeparam name="TMessage">Type of message</typeparam>
    public class YamlMessageDeserializer<TMessage> : MessageDeserializerBase<TMessage>
        where TMessage : class {
            private readonly IDeserializer _deserializer;

            private static readonly IDeserializer DEFAULT_DESERIALIZER = new DeserializerBuilder()
                                                                            .WithNamingConvention(new CamelCaseNamingConvention())
                                                                            .Build();

            /// <summary>
            /// Constructor
            /// </summary>
            public YamlMessageDeserializer() {
                _deserializer = DEFAULT_DESERIALIZER;
            }
            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="deserializer">Implementation of the deserializer to use</param>
            public YamlMessageDeserializer(IDeserializer deserializer) {
                if (deserializer == null) {
                    throw new ArgumentNullException(nameof(deserializer));
                }
                _deserializer = deserializer;
            }

            /// <inheritdoc />
            public override TMessage Deserialize(Stream stream) {
                using (var reader = new StreamReader(stream)) {
                    var content = reader.ReadToEnd();
                    var message = _deserializer.Deserialize<TMessage>(content);
                    return message;
                }
            }

        }
}