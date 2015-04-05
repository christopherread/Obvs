using Obvs.Configuration;

namespace Obvs.Serialization.Xml.Configuration
{
    public static class FluentConfigExtensions
    {
        public static ICanCreateEndpointAsClientOrServer SerializedAsXml(this ICanSpecifyEndpointSerializers config)
        {
            return config.SerializedWith(new XmlMessageSerializer(), new XmlMessageDeserializerFactory());
        }
    }
}