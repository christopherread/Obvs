namespace Obvs
{
    public interface IMessageDeserializer<out TMessage>
    {
        TMessage Deserialize(object obj);
        string GetTypeName();
    }

    public abstract class MessageDeserializerBase<TMessage> : IMessageDeserializer<TMessage>
    {
        public abstract TMessage Deserialize(object obj);

        public string GetTypeName()
        {
            return typeof(TMessage).Name;
        }
    }
}