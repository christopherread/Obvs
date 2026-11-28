using System;
using System.Linq;
using System.Threading;
using Apache.NMS;
using FakeItEasy;
using Obvs.ActiveMQ.Configuration;
using Xunit;

namespace Obvs.ActiveMQ.Tests
{
    public class TestConnectionFactoryExtensions
    {
        [Fact]
        public void ShouldConnectAgainWhenTheBrokerRefusedTheFirstConnection()
        {
            // A service can start before its broker does
            var factory = A.Fake<IConnectionFactory>();
            var connection = A.Fake<IConnection>();
            A.CallTo(() => factory.CreateConnection()).Throws(new NMSConnectionException("Connection refused")).Once()
                .Then.Returns(connection);
            var lazyConnection = factory.CreateLazyConnection();

            Assert.Throws<NMSConnectionException>(() => lazyConnection.Value);

            Assert.Same(connection, lazyConnection.Value);
            A.CallTo(() => connection.Start()).MustHaveHappened(1, Times.Exactly);
        }

        [Fact]
        public void ShouldOpenOneConnectionWhenManyConnectAtOnce()
        {
            // A service's sources and publishers share a connection, and subscribe at the same time
            var factory = A.Fake<IConnectionFactory>();
            A.CallTo(() => factory.CreateConnection()).ReturnsLazily(() =>
            {
                Thread.Sleep(20);
                return A.Fake<IConnection>();
            });
            var lazyConnection = factory.CreateLazyConnection();

            var connections = Enumerable.Range(0, 8).AsParallel().WithDegreeOfParallelism(8).Select(_ => lazyConnection.Value).ToList();

            Assert.Single(connections.Distinct());
            A.CallTo(() => factory.CreateConnection()).MustHaveHappened(1, Times.Exactly);
        }
    }
}
