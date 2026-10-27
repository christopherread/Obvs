using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Obvs.Serialization;
using Obvs.Serialization.Json;
using Obvs.Types;
using Xunit;

namespace Obvs.NetMQ.Tests
{
    // ZeroMQ drops messages published before a subscriber has connected (the "slow joiner"), so the
    // publisher's first message must wait for a subscriber. Ports are clear of the other NetMQ tests.
    public class TestSlowJoiner
    {
        private const string Topic = "SlowJoinerTopic";

        [Fact, Trait("Category", "Explicit")]
        public async Task FirstMessageReachesASubscriberThatConnectsLate()
        {
            const string address = "tcp://localhost:5570";
            var received = new ConcurrentQueue<IMessage>();
            var publisher = new MessagePublisher<IMessage>(address, new JsonMessageSerializer(), Topic);
            var source = new MessageSource<IMessage>(address, new IMessageDeserializer<IMessage>[] { new JsonMessageDeserializer<SlowJoinerMessage>() }, Topic);

            // The subscriber joins well after the publisher starts, as on a slow machine
            var subscribeLate = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(1.5), TestContext.Current.CancellationToken);
                return source.Messages.Subscribe(received.Enqueue);
            }, TestContext.Current.CancellationToken);

            await publisher.PublishAsync(new SlowJoinerMessage { Id = 1 }, TestContext.Current.CancellationToken);

            using (await subscribeLate)
            {
                await WaitFor(() => !received.IsEmpty, TimeSpan.FromSeconds(5));
            }
            publisher.Dispose();

            Assert.Contains(received, message => ((SlowJoinerMessage)message).Id == 1);
        }

        private static async Task WaitFor(Func<bool> condition, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (!condition() && DateTime.UtcNow < deadline)
            {
                await Task.Delay(50, TestContext.Current.CancellationToken);
            }
        }

        public class SlowJoinerMessage : IMessage
        {
            public int Id { get; set; }
        }
    }
}
