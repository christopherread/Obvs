using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using NATS.Client;
using Obvs.Serialization;
using Obvs.Serialization.Json;
using Xunit;

namespace Obvs.NATS.Tests
{
    public class TestSourceAndPublisher
    {
        [Fact, Trait("Category", "Explicit")]
        public async Task TestSendAndReceiveMessage()
        {
            var lazyConnection = new Lazy<IConnection>(() =>
            {
                var cf = new ConnectionFactory();
                return cf.CreateConnection(TestBroker.Url);
            }, LazyThreadSafetyMode.ExecutionAndPublication);

            const string subjectPrefix = "Obvs.NATS.Tests";
            var deserializers = new List<IMessageDeserializer<TestMessage>>
            {
                new JsonMessageDeserializer<TestMessage>()
            };
            IMessageSerializer serializer = new JsonMessageSerializer();

            var source = new MessageSource<TestMessage>(lazyConnection, subjectPrefix, deserializers);
            var publisher = new MessagePublisher<TestMessage>(lazyConnection, subjectPrefix, serializer);

            var received = new ConcurrentQueue<TestMessage>();
            var subscription = source.Messages.ObserveOn(Scheduler.Default).Subscribe(received.Enqueue);

            for (var i = 0; i < 10; i++)
            {
                await publisher.PublishAsync(new TestMessage {Id = i});
            }

            await Task.Delay(TimeSpan.FromSeconds(1));
            subscription.Dispose();

            Assert.Equal(Enumerable.Range(0, 10), received.Select(message => message.Id));
        }
    }
}
