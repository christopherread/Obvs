using System.IO;
using ProtoBuf;

namespace Obvs.Kafka
{
    [ProtoContract]
    public class KafkaHeaderedMessage
    {
        [ProtoMember(1)]
        public byte[] Payload { get; set; }

        [ProtoMember(2)]
        public string PayloadType { get; set; }

        public static Stream ToStream(byte[] data)
        {
            return new MemoryStream(data);
        }
    }
}