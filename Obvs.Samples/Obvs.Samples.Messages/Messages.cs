using Obvs.Types;

namespace Obvs.Samples.Messages
{
    public interface ITestMessage : IMessage { }

    public class TestCommand : ITestMessage, ICommand
    {
        public int Id { get; set; }
    }

    public class TestEvent : ITestMessage, IEvent
    {
        public int Id { get; set; }
    }
    public class TestRequest : ITestMessage, IRequest
    {
        public string RequestId { get; set; }
        public string Name { get; set; }
        public string RequesterId { get; set; }
    }

    public class TestResponse : ITestMessage, IResponse
    {
        public string RequestId { get; set; }

        public string RequesterId { get; set; }
    }
}
