using Newtonsoft.Json;
using Obvs.Hosting;
using Obvs.Serialization.Json;
using Obvs.Serialization.MessagePack;
using Obvs.Serialization.ProtoBuf;

namespace Obvs.Samples
{
    // The serializer packages' READMEs. Compiled, not run.
    public static class SerializationSamples
    {
        public static void Serializers(ObvsServiceBuilder service, JsonSerializerSettings settings)
        {
            // snippet: serialized-with-newtonsoft
            service.SerializedWith(new JsonMessageSerializer(settings),
                new JsonMessageDeserializerFactory(typeof(JsonMessageDeserializer<>), settings));
            // end-snippet

            // snippet: serialized-with-messagepack
            service.SerializedWith(new MessagePackCSharpMessageSerializer(), new MessagePackCSharpMessageDeserializerFactory());
            // end-snippet

            // snippet: serialized-with-protobuf
            service.SerializedWith(new ProtoBufMessageSerializer(), new ProtoBufMessageDeserializerFactory());
            // end-snippet
        }
    }
}
