using System.Collections.Generic;
using Obvs.Types;

namespace Obvs
{
    public class RequestPropertyProvider : IMessagePropertyProvider<IRequest>
    {
        public IEnumerable<KeyValuePair<string, object>> GetProperties(IRequest request)
        {
            return new List<KeyValuePair<string, object>>
            {
                new KeyValuePair<string, object>("RequestId", request.RequestId),
                new KeyValuePair<string, object>("RequesterId", request.RequesterId)
            };
        }
    }
}