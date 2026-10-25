using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Obvs.Kafka.Configuration;
using Obvs.Serialization;
using Obvs.Serialization.Json;
using Xunit;
using IMessage = Obvs.Types.IMessage;

namespace Obvs.Kafka.Tests
{
    public class TestMessageSendAndReceive
    {
        private IMessageDeserializer<ITestMessage> _deserializer;
        private IMessageSource<ITestMessage> _source;
        private IMessagePublisher<ITestMessage> _publisher;
        private IMessageSerializer _serializer;

        public interface ITestMessage : IMessage
        {
        }

        [XmlRoot]
        public class TestMessage : ITestMessage
        {
            public string Data { get; set; }

            public TestMessage()
            { 
            }

            public TestMessage(string data)
            {
                Data = data;
            }

            public override string ToString()
            {
                return Data;
            }
        }

        public TestMessageSendAndReceive()
        {
            // Run a local broker with: docker run -p 9092:9092 apache/kafka
            var cfg = new KafkaConfiguration(TestBroker.BootstrapServers);
            var sourceConfig = new KafkaSourceConfiguration();
            var producerConfig = new KafkaProducerConfiguration();
            const string topicName = "test-topic-3";

            _deserializer = new JsonMessageDeserializer<TestMessage>();
            _serializer = new JsonMessageSerializer();

            bool PropertyFilter(Dictionary<string, string> d) => true;
            Dictionary<string, string> PropertyProvider(ITestMessage message) => new Dictionary<string, string>();

            _source = new MessageSource<ITestMessage>(cfg, sourceConfig, topicName, new[] { _deserializer }, PropertyFilter);

            _publisher = new MessagePublisher<ITestMessage>(cfg, producerConfig, topicName, _serializer, PropertyProvider);
        }

        [Fact]
        [Trait("Category", "Explicit")]
        public async Task TestSendAndReceiveMessage()
        {
            var messages = new List<TestMessage>();
            var sub = _source.Messages
                .OfType<TestMessage>()
                .Subscribe(message =>
                {
                    Console.WriteLine($"Message received: {message}" );
                    messages.Add(message);
                }, 
                exception => Console.WriteLine(exception.ToString()));

            var delay1 = TimeSpan.FromSeconds(2);
            Console.WriteLine($"Waiting {delay1}...");
            await Task.Delay(delay1, TestContext.Current.CancellationToken);

            await _publisher.PublishAsync(new TestMessage($"hello world {DateTime.Now}"));
            await _publisher.PublishAsync(new TestMessage($"hello world {DateTime.Now}"));
            await _publisher.PublishAsync(new TestMessage($"hello world {DateTime.Now}"));

            var delay2 = TimeSpan.FromSeconds(2);
            Console.WriteLine($"Waiting {delay2}...");
            await Task.Delay(delay2, TestContext.Current.CancellationToken);

            Assert.NotEmpty(messages);
            Assert.Equal(3, messages.Count);
            Assert.True(messages[0].Data.StartsWith("hello world"), "incorrect message received");
            Assert.True(messages[1].Data.StartsWith("hello world"), "incorrect message received");
            Assert.True(messages[2].Data.StartsWith("hello world"), "incorrect message received");

            sub.Dispose();
        }

        [Fact]
        [Trait("Category", "Explicit")]
        public async Task TestMessagePublishedStraightAfterSubscribingIsReceived()
        {
            var data = $"straight after subscribing {Guid.NewGuid()}";
            var messages = new ConcurrentQueue<TestMessage>();

            using (_source.Messages.OfType<TestMessage>().Subscribe(messages.Enqueue))
            {
                // No delay: once Subscribe returns, a published message must not be missed
                await _publisher.PublishAsync(new TestMessage(data));

                var deadline = DateTime.UtcNow.AddSeconds(10);
                while (!messages.Any(message => message.Data == data) && DateTime.UtcNow < deadline)
                {
                    await Task.Delay(100, TestContext.Current.CancellationToken);
                }
            }

            Assert.Contains(messages, message => message.Data == data);
        }

        [Fact]
        [Trait("Category", "Explicit")]
        public async Task ShouldNotDeadlockWhenASubscriberDisposesItsSubscription()
        {
            // Code that runs on receiving a message may shut the app down. If messages arrived on the consume
            // thread, disposing the subscription there would wait for that same thread, forever.
            var received = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var disposed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            IDisposable subscription = null;
            subscription = _source.Messages.OfType<TestMessage>().Subscribe(_ =>
            {
                received.TrySetResult(true);
                subscription.Dispose();
                disposed.TrySetResult(true);
            });

            await _publisher.PublishAsync(new TestMessage($"dispose in callback {Guid.NewGuid()}"));

            Assert.True(await received.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
            Assert.True(await disposed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        }
    }
}