using Obvs.Configuration;

namespace Obvs.Serialization.ProtoBuf.Configuration
{
    public static class FluentConfigExtensions
    {
        public static ICanCreateEndpointAsClientOrServer SerializedAsProtoBuf(this ICanSpecifyEndpointSerializers config)
        {
            return config.SerializedWith(new ProtoBufMessageSerializer(), new ProtoBufMessageDeserializerFactory());
        }
    }
}