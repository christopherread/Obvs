using Obvs.Types;

namespace Obvs.Extensions
{
    internal static class ResponseExtensions
    {
        public static bool CorrelatesTo(this IResponse response, IRequest request)
        {
            return response.RequestId == request.RequestId &&
                   response.RequesterId == request.RequesterId;
        }

        public static void SetCorrelationIds(this IResponse response, IRequest request)
        {
            response.RequestId = request.RequestId;
            response.RequesterId = request.RequesterId;
        }
    }
}