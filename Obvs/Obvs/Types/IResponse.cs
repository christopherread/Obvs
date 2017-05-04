namespace Obvs.Types
{
    public interface IResponse : IMessage
    {
        string RequestId { get; set; }
        string RequesterId { get; set; }
    }
}