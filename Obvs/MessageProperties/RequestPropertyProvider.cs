using System.Collections.Generic;
using Obvs.Types;

// v6's message property providers, kept for v6's fluent configuration: its public members are exempt from the rule that every one is documented
#pragma warning disable CS1570, CS1572, CS1591

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