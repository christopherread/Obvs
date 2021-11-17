using Obvs.Types;

namespace Obvs.RabbitMQ.Tests.Messages
{
    public class TestEvent : IEvent, ITestMessage
    {
        public int Id { get; set; }
    }
}