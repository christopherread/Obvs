using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Threading;
using NUnit.Framework;
using Obvs.Serialization.Json;
using Obvs.Serialization.ProtoBuf;
using Obvs.Types;
using ProtoBuf;
using RabbitMQ.Client;

namespace Obvs.RabbitMQ.Tests
{
    [TestFixture]
    public class TestSendingAndReceiving
    {
        [ProtoContract]
        public class TestMessage : IMessage
        {
            [ProtoMember(1)]
            public DateTime Timestamp { get; set; }

            [ProtoMember(2)]
            public string Data { get; set; }

            public TestMessage()
            {
                Timestamp = DateTime.Now;
            }

            public override string ToString()
            {
                return string.Format("TestMessage(Timestamp={0},Data={1})", Timestamp.ToString("G"), Data);
            }
        }

        [Test, Explicit]
        public void TestSendReceiveAsJson()
        {
            IConnectionFactory connectionFactory = new ConnectionFactory {HostName = "localhost"};
            string exchange = GetType().Name;

            IMessageSource<IMessage> messageSource =
                new MessageSource<IMessage>(connectionFactory,
                    new List<IMessageDeserializer<TestMessage>> {new JsonMessageDeserializer<TestMessage>()},
                    exchange);

            IMessagePublisher<IMessage> messagePublisher = new MessagePublisher<IMessage>(connectionFactory,
                new JsonMessageSerializer(), exchange);

            List<TestMessage> receivedMessages = new List<TestMessage>();
            messageSource.Messages.OfType<TestMessage>().Subscribe(msg => { Console.WriteLine(msg); receivedMessages.Add(msg); }, Console.WriteLine, () => Console.WriteLine("Completed!"));

            List<TestMessage> messages = new List<TestMessage>
            {
                new TestMessage {Data = "Hello"},
                new TestMessage {Data = "World!"},
                new TestMessage {Data = "..."},
                new TestMessage {Data = "Good"},
                new TestMessage {Data = "Bye"},
                new TestMessage {Data = "Cruel"},
                new TestMessage {Data = "World"}
            };
            foreach (TestMessage message in messages)
            {
                messagePublisher.Publish(message);
            }

            Thread.Sleep(TimeSpan.FromSeconds(1));

            Assert.That(receivedMessages.Count, Is.EqualTo(7), "Incorrect number of messages received");

            for (int index = 0; index < messages.Count; index++)
            {
                TestMessage message = messages[index];
                TestMessage received = receivedMessages[index];
                Assert.That(received.Data == message.Data && received.Timestamp == message.Timestamp, string.Format("Incorrect message: {0}", received));
            }
        }
        
        [Test, Explicit]
        public void TestSendReceiveAsProfoBuf()
        {
            IConnectionFactory connectionFactory = new ConnectionFactory {HostName = "localhost"};
            string exchange = GetType().Name;

            IMessageSource<IMessage> messageSource =
                new MessageSource<IMessage>(connectionFactory,
                    new List<IMessageDeserializer<TestMessage>> {new ProtoBufMessageDeserializer<TestMessage>()},
                    exchange);

            IMessagePublisher<IMessage> messagePublisher = new MessagePublisher<IMessage>(connectionFactory,
                new ProtoBufMessageSerializer(), exchange);

            List<TestMessage> receivedMessages = new List<TestMessage>();
            messageSource.Messages.OfType<TestMessage>().Subscribe(msg => { Console.WriteLine(msg); receivedMessages.Add(msg); }, Console.WriteLine, () => Console.WriteLine("Completed!"));

            List<TestMessage> messages = new List<TestMessage>
            {
                new TestMessage {Data = "Hello"},
                new TestMessage {Data = "World!"},
                new TestMessage {Data = "..."},
                new TestMessage {Data = "Good"},
                new TestMessage {Data = "Bye"},
                new TestMessage {Data = "Cruel"},
                new TestMessage {Data = "World"}
            };
            foreach (TestMessage message in messages)
            {
                messagePublisher.Publish(message);
            }

            Thread.Sleep(TimeSpan.FromSeconds(1));

            Assert.That(receivedMessages.Count, Is.EqualTo(7), "Incorrect number of messages received");

            for (int index = 0; index < messages.Count; index++)
            {
                TestMessage message = messages[index];
                TestMessage received = receivedMessages[index];
                Assert.That(received.Data == message.Data && received.Timestamp == message.Timestamp, string.Format("Incorrect message: {0}", received));
            }
        }
    }
}
