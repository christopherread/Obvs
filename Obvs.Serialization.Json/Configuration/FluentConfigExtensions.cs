using Obvs.Configuration;

namespace Obvs.Serialization.Json.Configuration
{
    public static class FluentConfigExtensions
    {
        public static ICanCreateEndpointAsClientOrServer<TMessage, TCommand, TEvent, TRequest, TResponse> SerializedAsJson<TMessage, TCommand, TEvent, TRequest, TResponse>(
            this ICanSpecifyEndpointSerializers<TMessage, TCommand, TEvent, TRequest, TResponse> config,
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
                    new GzippedJsonMessageSerializer(), 
                    new JsonMessageDeserializerFactory(typeof(GzippedJsonMessageDeserializer<>)));
            }

            return config.SerializedWith(
                new JsonMessageSerializer(),
                new JsonMessageDeserializerFactory(typeof(JsonMessageDeserializer<>)));
        }
    }
}