using System.Collections.Generic;
using ProtoBuf;

// v6's NATS transport building blocks, which UseNats(...) is built on: its public members are exempt from the rule that every one is documented
#pragma warning disable CS1570, CS1572, CS1591

namespace Obvs.NATS
{
    /// <summary>
    /// Enables support of message properties on NATS
    /// </summary>
    [ProtoContract]
    public class MessageWrapper
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