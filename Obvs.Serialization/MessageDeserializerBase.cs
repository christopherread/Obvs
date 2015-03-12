namespace Obvs.Serialization
{
    public abstract class MessageDeserializerBase<TMessage> : IMessageDeserializer<TMessage>
    {
        public abstract TMessage Deserialize(object obj);

        public string GetTypeName()
        {
            return typeof(TMessage).Name;
        }
    }
}