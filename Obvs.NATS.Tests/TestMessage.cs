using System;
using Obvs.Types;

namespace Obvs.NATS.Tests
{
    public interface ITestService { }

    public class TestMessage : IMessage, ITestService
    {
        public int Id { get; set; }
        public DateTime CreatedAt { get; set; }

        public TestMessage()
        {
            CreatedAt = DateTime.Now;
        }

        public override string ToString()
        {
            return string.Format("{2}[Id={0},CreatedAt={1}]", Id, CreatedAt.ToString("HH:mm:ss"), GetType().Name);
        }
    }

    public class TestCommand : TestMessage, ICommand
    {
    }

    public class TestEvent : TestMessage, IEvent
    {
    }

    public class TestRequest : TestMessage, IRequest
    {
        public string RequestId { get; set; }
        public string RequesterId { get; set; }
    }

    public class TestResponse : TestMessage, IResponse
    {
        public string RequestId { get; set; }
        public string RequesterId { get; set; }
    }
}