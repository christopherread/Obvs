using System;
using Obvs.Configuration;
using Obvs.Types;

namespace Obvs.Extensions
{
    internal static class RequestExtensions
    {
        public static void SetCorrelationIds(this IRequest request)
        {
            if (string.IsNullOrEmpty(request.RequestId))
            {
                request.RequestId = Guid.NewGuid().ToString();
            }
            request.RequesterId = RequesterId.Create();
        }
    }
}