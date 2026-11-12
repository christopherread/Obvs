namespace Obvs.Types
{
    /// <summary>A message. Each service's messages share an interface derived from this, and are each one of ICommand, IEvent, IRequest or IResponse.</summary>
    public interface IMessage
    {
    }
}