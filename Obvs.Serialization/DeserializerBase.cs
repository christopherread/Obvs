namespace Obvs.Serialization
{
    public abstract class DeserializerBase<TMessage>
    {
        public string GetTypeName()
        {
            return typeof(TMessage).Name;
        }
    }
}