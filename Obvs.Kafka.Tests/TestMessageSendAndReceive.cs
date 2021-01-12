using System;
using System.Collections.Generic;
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
            // Steps to run local kafka broker:
            // - Install: Docker for Windows / Docker Desktop
            // - Clone: https://github.com/wurstmeister/kafka-docker
            // - Edit: docker-compose-single-broker.yml
            // - Change: KAFKA_ADVERTISED_HOST_NAME: localhost
            // - Run: docker-compose -f "docker-compose-single-broker.yml" up -d --build
            var cfg = new KafkaConfiguration("localhost:9092");
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
            await Task.Delay(delay1);

            await _publisher.PublishAsync(new TestMessage($"hello world {DateTime.Now}"));
            await _publisher.PublishAsync(new TestMessage($"hello world {DateTime.Now}"));
            await _publisher.PublishAsync(new TestMessage($"hello world {DateTime.Now}"));

            var delay2 = TimeSpan.FromSeconds(2);
            Console.WriteLine($"Waiting {delay2}...");
            await Task.Delay(delay2);

            Assert.NotEmpty(messages);
            Assert.Equal(3, messages.Count);
            Assert.True(messages[0].Data.StartsWith("hello world"), "incorrect message received");
            Assert.True(messages[1].Data.StartsWith("hello world"), "incorrect message received");
            Assert.True(messages[2].Data.StartsWith("hello world"), "incorrect message received");

            sub.Dispose();
        }
    }
}