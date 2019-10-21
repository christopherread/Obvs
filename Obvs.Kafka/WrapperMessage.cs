using System.Collections.Generic;
using System.IO;
using ProtoBuf;

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