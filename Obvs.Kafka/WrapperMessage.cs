using System.Collections.Generic;
using System.IO;
using ProtoBuf;

namespace Obvs.Kafka
{
    // A message's body, with its type's name and the properties v6 filtered on, as it goes over Kafka
    [ProtoContract]
    internal class WrapperMessage
    {
        [ProtoMember(1)]
        public byte[] Payload { get; set; }

        [ProtoMember(2)]
        public string PayloadType { get; set; }

        [ProtoMember(3)]
        public Dictionary<string, string> Properties { get; set; }
    }
}