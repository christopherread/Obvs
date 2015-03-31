namespace Obvs.Serialization
{
    public interface IMessageSerializer
    {
        object Serialize(object message);
    }
}