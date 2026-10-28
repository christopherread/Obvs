namespace Obvs.Types
{
    /// <summary>
    /// A request that's answered with a <typeparamref name="TResponse"/>. <c>RequestAsync</c> returns that type, and
    /// <c>HandleRequest</c> must answer with it.
    /// </summary>
    /// <typeparam name="TResponse">The response that answers the request</typeparam>
    public interface IRequest<TResponse> : IRequest
        where TResponse : IResponse
    {
    }
}
