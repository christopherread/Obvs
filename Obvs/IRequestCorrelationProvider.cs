using System;
using Obvs.Configuration;
using Obvs.Types;

namespace Obvs
{
    /// <summary>Correlates responses with requests, for the bus's standard message interfaces.</summary>
    public interface IRequestCorrelationProvider : IRequestCorrelationProvider<IRequest, IResponse>
    {
    }

    /// <summary>Correlates responses with the requests they answer.</summary>
    public interface IRequestCorrelationProvider<in TRequest, in TResponse>
        where TRequest : class
        where TResponse : class
    {
        /// <summary>Gives a request its IDs, before it's sent.</summary>
        void SetRequestCorrelationIds(TRequest request);
        /// <summary>Gives a response its request's IDs, before it's sent.</summary>
        void SetCorrelationIds(TRequest request, TResponse response);
        /// <summary>Whether <paramref name="response"/> answers <paramref name="request"/>.</summary>
        bool AreCorrelated(TRequest request, TResponse response);
    }

    /// <summary>Correlates by <see cref="Types.IRequest.RequestId"/> and <see cref="Types.IRequest.RequesterId"/>: a new GUID and this process's <see cref="Configuration.RequesterId"/>.</summary>
    public class DefaultRequestCorrelationProvider : IRequestCorrelationProvider
    {
        /// <inheritdoc />
        public void SetRequestCorrelationIds(IRequest request)
        {
            if(string.IsNullOrEmpty(request.RequestId))
            {
                request.RequestId = Guid.NewGuid().ToString();
            }
            
            if(string.IsNullOrEmpty(request.RequesterId))
            {
                request.RequesterId = RequesterId.Create();
            }
        }

        /// <inheritdoc />
        public bool AreCorrelated(IRequest request, IResponse response)
        {
            return request.RequestId == response.RequestId &&
                   request.RequesterId == response.RequesterId;
        }

        /// <inheritdoc />
        public void SetCorrelationIds(IRequest request, IResponse response)
        {
            response.RequestId = request.RequestId;
            response.RequesterId = request.RequesterId;
        }
    }
}
