using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FakeItEasy;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using Obvs.NATS.Tests.Hosting;
using Obvs.Serialization.Json;
using Xunit;

namespace Obvs.NATS.Tests
{
    public class TestMessagePublisherCancellation
    {
        [Fact]
        public async Task ShouldPassTheTokenToNats()
        {
            var connection = A.Fake<INatsConnection>();
            var publisher = new MessagePublisher<IOrderMessage>(connection, "orders.Commands", new JsonMessageSerializer());
            using var cancellation = new CancellationTokenSource();

            await publisher.PublishAsync(new PlaceOrder { OrderId = "o-1" }, new Dictionary<string, string>(), cancellation.Token);

            A.CallTo(() => connection.PublishAsync(A<string>._, A<byte[]>._, A<NatsHeaders>._, A<string>._, A<INatsSerialize<byte[]>>._,
                A<NatsPubOpts>._, cancellation.Token)).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public async Task ShouldPassTheTokenToJetStream()
        {
            var jetStream = A.Fake<INatsJSContext>();
            A.CallTo(() => jetStream.PublishAsync(A<string>._, A<byte[]>._, A<INatsSerialize<byte[]>>._, A<NatsJSPubOpts>._, A<NatsHeaders>._,
                A<CancellationToken>._)).Returns(new PubAckResponse());
            var publisher = new Obvs.NATS.JetStream.MessagePublisher<IOrderMessage>(jetStream, "orders.Commands", new JsonMessageSerializer());
            using var cancellation = new CancellationTokenSource();

            await publisher.PublishAsync(new PlaceOrder { OrderId = "o-1" }, new Dictionary<string, string>(), cancellation.Token);

            A.CallTo(() => jetStream.PublishAsync(A<string>._, A<byte[]>._, A<INatsSerialize<byte[]>>._, A<NatsJSPubOpts>._, A<NatsHeaders>._,
                cancellation.Token)).MustHaveHappenedOnceExactly();
        }
    }
}
