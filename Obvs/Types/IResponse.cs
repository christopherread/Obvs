namespace Obvs.Types
{
    /// <summary>A response: the answer to an <see cref="IRequest"/>, sent back to whoever sent the request.</summary>
    public interface IResponse : IMessage
    {
        /// <summary>The ID of the request this answers. The bus sets it when replying.</summary>
        string RequestId { get; set; }
        /// <summary>The ID of the process that sent the request. The bus sets it when replying.</summary>
        string RequesterId { get; set; }
    }
}