using System;
using System.Threading;
using System.Threading.Tasks;
using Obvs.Serialization.Json;
using Obvs.Types;
using Xunit;

namespace Obvs.NetMQ.Tests
{
    public class TestMessagePublisherCancellation
    {
        [Fact]
        public async Task ShouldNotPublishWhenAlreadyCancelled()
        {
            // Publishing would bind the socket and wait for a subscriber
            var publisher = new MessagePublisher<IMessage>("tcp://localhost:5599", new JsonMessageSerializer(), "orders");

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => publisher.PublishAsync(new TestPublishSubscribe.TestMessage1(), new CancellationToken(true)));

            publisher.Dispose();
        }
    }
}
