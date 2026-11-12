using System.Collections.Generic;
using ProtoBuf;

namespace Obvs.NATS
{
    // A message's body, with the properties v6 filtered on, as it goes over NATS
    [ProtoContract]
    internal class MessageWrapper
    {
        public MessageWrapper()
        {
        }

        public MessageWrapper(Dictionary<string, string> properties, byte[] body)
        {
            Properties = properties;
            Body = body;
        }

        [ProtoMember(1)]
        public Dictionary<string, string> Properties { get; set; }
        [ProtoMember(2)]
        public byte[] Body { get; set; }
    }
}