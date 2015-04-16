using System;
using Obvs.Configuration;
using Obvs.Types;

namespace Obvs
{
    public interface IRequestCorrelationProvider
    {
        void ProvideRequestCorrelationIds(IRequest request);
    }

    public class DefaultRequestCorrelationProvider : IRequestCorrelationProvider
    {
        public void ProvideRequestCorrelationIds(IRequest request)
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
    }
}
