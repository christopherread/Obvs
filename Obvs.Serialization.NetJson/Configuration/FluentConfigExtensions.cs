using Obvs.Configuration;

namespace Obvs.Serialization.NetJson.Configuration
{
    public static class FluentConfigExtensions
    {
        public static ICanCreateEndpointAsClientOrServer<TMessage, TCommand, TEvent, TRequest, TResponse> SerializedAsNetJson<TMessage, TCommand, TEvent, TRequest, TResponse>(
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
                    new GzippedNetJsonMessageSerializer(),
                    new NetJsonMessageDeserializerFactory(typeof(GzippedNetJsonMessageDeserializer<>)));
            }

            return config.SerializedWith(
                new NetJsonMessageSerializer(),
                new NetJsonMessageDeserializerFactory(typeof(NetJsonMessageDeserializer<>)));
        }
    }
}