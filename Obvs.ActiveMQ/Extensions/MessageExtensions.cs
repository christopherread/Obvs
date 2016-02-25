using Apache.NMS;
using Obvs.MessageProperties;

namespace Obvs.ActiveMQ.Extensions
{
    internal static class MessageExtensions
    {
        public static bool TryGetTypeName(this IMessage message, out string typeName)
        {
            typeName = message.Properties.GetString(MessagePropertyNames.TypeName);
            return typeName != null;
        }
    }
}