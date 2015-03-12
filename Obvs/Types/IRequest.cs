namespace Obvs.Types
{
    public interface IRequest : IMessage
    {
        string RequestId { get; set; }
        string RequesterId { get; set; }
    }
}