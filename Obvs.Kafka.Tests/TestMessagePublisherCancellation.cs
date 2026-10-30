using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Obvs.Kafka.Configuration;
using Obvs.Kafka.Tests.Hosting;
using Obvs.Serialization.Json;
using Xunit;

namespace Obvs.Kafka.Tests
{
    public class TestMessagePublisherCancellation
    {
        [Fact]
        public async Task ShouldStopWaitingForTheBrokerWhenCancelled()
        {
            // Nothing listens on port 1, so the message waits for a broker until it's cancelled. The publisher isn't
            // disposed: disposing flushes, which would wait for the undeliverable message to time out.
            var publisher = new MessagePublisher<IOrderMessage>(new KafkaConfiguration("127.0.0.1:1"), new KafkaProducerConfiguration(),
                "orders.Commands", new JsonMessageSerializer(), null);
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

            var publishing = publisher.PublishAsync(new PlaceOrder { OrderId = "o-1" }, new Dictionary<string, string>(), cancellation.Token);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => publishing.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        }
    }
}
