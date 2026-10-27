using System.Collections.Generic;
using System.IO;
using ProtoBuf;

// v6's Kafka transport building blocks, public for v6's fluent configuration; v7 apps use UseKafka(...): its public members are exempt from the rule that every one is documented
#pragma warning disable CS1570, CS1572, CS1591

namespace Obvs.Kafka
{
    /// <summary>
    /// Message wrapper used to append properties and type information 
    /// to message before sending to Kafka
    /// </summary>
    [ProtoContract]
    public class WrapperMessage
    {
        [ProtoMember(1)]
        public byte[] Payload { get; set; }

        [ProtoMember(2)]
        public string PayloadType { get; set; }

        [ProtoMember(3)]
        public Dictionary<string, string> Properties { get; set; }
    }
}