using Obvs.Configuration;

namespace Obvs.Serialization.Xml.Configuration
{
    public static class FluentConfigExtensions
    {
        public static ICanCreateEndpointAsClientOrServer<TMessage, TCommand, TEvent, TRequest, TResponse> SerializedAsXml<TMessage, TCommand, TEvent, TRequest, TResponse>(this ICanSpecifyEndpointSerializers<TMessage, TCommand, TEvent, TRequest, TResponse> config)
            where TMessage : class
            where TCommand : class, TMessage
            where TEvent : class, TMessage
            where TRequest : class, TMessage
            where TResponse : class, TMessage
        {
            return config.SerializedWith(new XmlMessageSerializer(), new XmlMessageDeserializerFactory());
        }
    }
}