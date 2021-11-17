using Obvs.Types;

namespace Obvs.RabbitMQ.Tests.Messages
{
    public class TestRequest : IRequest, ITestMessage
    {
        public string RequestId { get; set; }
        public string RequesterId { get; set; }
        public int Id { get; set; }
    }
}