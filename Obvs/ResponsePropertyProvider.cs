using System.Collections.Generic;
using Obvs.Types;

namespace Obvs
{
    public class ResponsePropertyProvider : IMessagePropertyProvider<IResponse>
    {
        public IEnumerable<KeyValuePair<string, object>> GetProperties(IResponse response)
        {
            return new List<KeyValuePair<string, object>>
            {
                new KeyValuePair<string, object>("RequestId", response.RequestId),
                new KeyValuePair<string, object>("RequesterId", response.RequesterId)
            };
        }
    }
}