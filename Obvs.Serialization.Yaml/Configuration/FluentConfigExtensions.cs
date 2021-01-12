using YamlDotNet.Serialization;
using Obvs.Configuration;

namespace Obvs.Serialization.Yaml.Configuration
{
    public static class YamlFluentConfigExtensions
    {
        public static ICanCreateEndpointAsClientOrServer<TMessage, TCommand, TEvent, TRequest, TResponse> SerializedAsYaml<TMessage, TCommand, TEvent, TRequest, TResponse>(
            this ICanSpecifyEndpointSerializers<TMessage, TCommand, TEvent, TRequest, TResponse> config) 
            where TMessage : class 
            where TCommand : class, TMessage
            where TEvent : class, TMessage 
            where TRequest : class, TMessage 
            where TResponse : class, TMessage
        {
            return config.SerializedWith(
                new YamlMessageSerializer(),
                new YamlMessageDeserializerFactory(typeof(YamlMessageDeserializer<>)));
        }
    }
}