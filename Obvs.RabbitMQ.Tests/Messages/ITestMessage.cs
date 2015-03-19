using System;
using Obvs.Types;
using ProtoBuf;

namespace Obvs.RabbitMQ.Tests.Messages
{
    public interface ITestMessage : IMessage { }

    [ProtoContract]
    public class TestMessage : ITestMessage
    {
        [ProtoMember(1)]
        public DateTime Timestamp { get; set; }

        [ProtoMember(2)]
        public string Data { get; set; }

        public TestMessage()
        {
            Timestamp = DateTime.Now;
        }

        public override string ToString()
        {
            return string.Format("TestMessage(Timestamp={0},Data={1})", Timestamp.ToString("G"), Data);
        }
    }
}