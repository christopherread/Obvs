using Obvs.Configuration;

namespace Obvs.Serialization.Json.Configuration
{
    public static class FluentConfigExtensions
    {
        public static ICanCreateEndpointAsClientOrServer SerializedAsJson(this ICanSpecifyEndpointSerializers config)
        {
            return config.SerializedWith(new JsonMessageSerializer(), new JsonMessageDeserializerFactory());
        }
    }
}