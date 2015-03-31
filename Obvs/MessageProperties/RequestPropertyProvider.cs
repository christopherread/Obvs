using System.Collections.Generic;
using Obvs.Types;

namespace Obvs.MessageProperties
{
    public class RequestPropertyProvider : IMessagePropertyProvider<IRequest>
    {
        public IEnumerable<KeyValuePair<string, object>> GetProperties(IRequest request)
        {
            return new List<KeyValuePair<string, object>>
            {
                new KeyValuePair<string, object>(MessagePropertyNames.RequestId, request.RequestId),
                new KeyValuePair<string, object>(MessagePropertyNames.RequesterId, request.RequesterId)
            };
        }
    }
}