using Obvs.Types;

namespace Obvs
{
    public interface IHandle<in TMessage> where TMessage : IMessage
    {
        void Handle(TMessage message);
    }
}