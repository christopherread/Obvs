using System.Collections.Generic;
using ProtoBuf;

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