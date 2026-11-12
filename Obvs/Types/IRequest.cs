namespace Obvs.Types
{
    /// <summary>A request: a question for a service, which one of its servers answers with an <see cref="IResponse"/>.</summary>
    public interface IRequest : IMessage
    {
        /// <summary>Identifies the request, so its responses find it. The bus sets it if it's empty.</summary>
        string RequestId { get; set; }
        /// <summary>Identifies the process that sent the request. The bus sets it if it's empty.</summary>
        string RequesterId { get; set; }
    }
}