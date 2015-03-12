namespace Obvs
{
    public interface IMessageSerializer
    {
        object Serialize(object message);
    }
}