using System;
using Obvs.Configuration;
using Obvs.Types;

namespace Obvs
{
    public interface IRequestCorrelationProvider : IRequestCorrelationProvider<IRequest, IResponse>
    {
    }

    public interface IRequestCorrelationProvider<in TRequest, in TResponse>
        where TRequest : class
        where TResponse : class
    {
        void SetRequestCorrelationIds(TRequest request);
        void SetCorrelationIds(TRequest request, TResponse response);
        bool AreCorrelated(TRequest request, TResponse response);
    }

    public class DefaultRequestCorrelationProvider : IRequestCorrelationProvider<IRequest, IResponse>
    {
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

        public bool AreCorrelated(IRequest request, IResponse response)
        {
            return request.RequestId == response.RequestId &&
                   request.RequesterId == response.RequesterId;
        }

        public void SetCorrelationIds(IRequest request, IResponse response)
        {
            response.RequestId = request.RequestId;
            response.RequesterId = request.RequesterId;
        }
    }
}
