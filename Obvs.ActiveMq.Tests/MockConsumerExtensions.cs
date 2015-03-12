using Apache.NMS;
using FakeItEasy;
using Moq;

namespace Obvs.ActiveMq.Tests
{
    internal static class MockConsumerExtensions
    {
        // use Moq for consumer as FakeItEasy doesnt support raising non-EventArg events
        // https://github.com/FakeItEasy/FakeItEasy/issues/30
        public static Mock<IMessageConsumer> Create(ISession session, IDestination destination)
        {
            Mock<IMessageConsumer> mockConsumer = new Mock<IMessageConsumer>();
            A.CallTo(() => session.CreateConsumer(destination)).Returns(mockConsumer.Object);
            return mockConsumer;
        }

        public static void RaiseFakeMessage(this Mock<IMessageConsumer> mockConsumer, IMessage message)
        {
            // fake a message coming off MQ queue
            mockConsumer.Raise(mock => mock.Listener += null, message);
        }
    }
}