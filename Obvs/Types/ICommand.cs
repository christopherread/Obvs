namespace Obvs.Types
{
    /// <summary>A command: something a service should do. It's sent to the service's servers, and one of them handles it.</summary>
    public interface ICommand : IMessage
    {
    }
}