using Obvs.Types;

namespace Obvs.RabbitMQ.Tests.Messages
{
    public class TestCommand : ICommand, ITestMessage
    {
        public int Id { get; set; }
    }
}