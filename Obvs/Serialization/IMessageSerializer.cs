using System.IO;

namespace Obvs.Serialization
{
    /// <summary>Helpers for serializers.</summary>
    public static class MessageSerializerExtentions
    {
        /// <summary>Serializes a message to bytes.</summary>
        public static byte[] Serialize(this IMessageSerializer serializer, object obj)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                serializer.Serialize(ms, obj);

                return ms.ToArray();
            }
        }
    }

    /// <summary>Writes messages, for a transport to send.</summary>
    public interface IMessageSerializer
    {
        /// <summary>Writes <paramref name="message"/> to <paramref name="destination"/>.</summary>
        void Serialize(Stream destination, object message);
    }
}