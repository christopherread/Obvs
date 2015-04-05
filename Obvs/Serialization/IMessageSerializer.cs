using System.IO;

namespace Obvs.Serialization
{
    public interface IMessageSerializer
    {
        object Serialize(object message);
        void Serialize(Stream destination, object message);
    }
}