using System;
using Obvs.Configuration;
using Obvs.Types;

namespace Obvs
{
    public interface IRequestCorrelationProvider
    {
        void SetRequestCorrelationIds(IRequest request);
    }

    public class DefaultRequestCorrelationProvider : IRequestCorrelationProvider
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
    }
}
