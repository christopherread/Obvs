using System;
using System.Threading;
using System.Threading.Tasks;
using FakeItEasy;
using Obvs.RabbitMQ.Tests.Hosting;
using Obvs.Serialization.Json;
using RabbitMQ.Client;
using Xunit;

namespace Obvs.RabbitMQ.Tests
{
    public class TestMessagePublisherCancellation
    {
        [Fact]
        public async Task ShouldPassTheTokenToRabbitMq()
        {
            var connection = A.Fake<IConnection>();
            var channel = A.Fake<IChannel>();
            A.CallTo(() => connection.CreateChannelAsync(A<CreateChannelOptions>._, A<CancellationToken>._)).Returns(channel);
            var publisher = new MessagePublisher<IOrderMessage>(new Lazy<IConnection>(() => connection), new JsonMessageSerializer(), "orders", "orders.Commands");
            using var cancellation = new CancellationTokenSource();

            await publisher.PublishAsync(new PlaceOrder { OrderId = "o-1" }, cancellation.Token);

            A.CallTo(() => channel.BasicPublishAsync(A<string>._, A<string>._, A<bool>._, A<BasicProperties>._, A<ReadOnlyMemory<byte>>._,
                cancellation.Token)).MustHaveHappenedOnceExactly();
        }
    }
}
