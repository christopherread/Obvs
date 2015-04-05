using Obvs.Configuration;

namespace Obvs.Serialization.MessagePack.Configuration
{
    public static class FluentConfigExtensions
    {
        public static ICanCreateEndpointAsClientOrServer SerializedAsMsgPack(this ICanSpecifyEndpointSerializers config)
        {
            return config.SerializedWith(new MsgPackMessageSerializer(), new MsgPackMessageDeserializerFactory());
        }
    }
}