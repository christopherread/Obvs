using Newtonsoft.Json;
using Obvs.Configuration;

namespace Obvs.Serialization.Json.Configuration
{
    public static class FluentConfigExtensions
    {
        public static ICanCreateEndpointAsClientOrServer<TMessage, TCommand, TEvent, TRequest, TResponse> SerializedAsJson<TMessage, TCommand, TEvent, TRequest, TResponse>(
            this ICanSpecifyEndpointSerializers<TMessage, TCommand, TEvent, TRequest, TResponse> config,
            JsonSerializerSettings serializerSettings = null,
            bool gzipped = false) 
            where TMessage : class 
            where TCommand : class, TMessage
            where TEvent : class, TMessage 
            where TRequest : class, TMessage 
            where TResponse : class, TMessage
        {
            if (gzipped)
            {
                return config.SerializedWith(
                    new GzippedJsonMessageSerializer(serializerSettings), 
                    new JsonMessageDeserializerFactory(typeof(GzippedJsonMessageDeserializer<>), serializerSettings));
            }

            return config.SerializedWith(
                new JsonMessageSerializer(serializerSettings),
                new JsonMessageDeserializerFactory(typeof(JsonMessageDeserializer<>), serializerSettings));
        }
    }
}