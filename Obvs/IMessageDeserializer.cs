namespace Obvs
{
    public interface IMessageDeserializer<out TMessage>
    {
        TMessage Deserialize(object obj);
        string GetTypeName();
    }
}