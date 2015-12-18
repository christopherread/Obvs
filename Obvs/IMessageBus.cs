namespace Obvs
{
    public interface IMessageBus<TMessage> : IMessagePublisher<TMessage>, IMessageSource<TMessage> 
        where TMessage : class 
    {
    }
}