using System.IO;

namespace Obvs.Serialization
{
    public static class MessageSerializerExtentions
    {
        public static byte[] Serialize(this IMessageSerializer serializer, object obj)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                serializer.Serialize(ms, obj);

                return ms.ToArray();
            }
        }
    }

    public interface IMessageSerializer
    {
        void Serialize(Stream destination, object message);
    }
}