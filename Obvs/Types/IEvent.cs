namespace Obvs.Types
{
    /// <summary>An event: something that happened. A service publishes it to every client that listens.</summary>
    public interface IEvent : IMessage
    {
    }
}