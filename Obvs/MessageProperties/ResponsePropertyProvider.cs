using System.Collections.Generic;
using Obvs.Types;

namespace Obvs.MessageProperties
{
    public class ResponsePropertyProvider : IMessagePropertyProvider<IResponse>
    {
        public IEnumerable<KeyValuePair<string, object>> GetProperties(IResponse response)
        {
            return new List<KeyValuePair<string, object>>
            {
                new KeyValuePair<string, object>(MessagePropertyNames.RequestId, response.RequestId),
                new KeyValuePair<string, object>(MessagePropertyNames.RequesterId, response.RequesterId)
            };
        }
    }
}