using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Threading;
using NUnit.Framework;
using Obvs.RabbitMQ.Tests.Messages;
using Obvs.Serialization.Json;
using Obvs.Serialization.ProtoBuf;
using Obvs.Types;
using RabbitMQ.Client;

namespace Obvs.RabbitMQ.Tests
{
    [TestFixture]
    public class TestSendingAndReceiving
    {
        [Test, Explicit]
        [TestCase("Json")]
        [TestCase("ProtoBuf")]
        public void TestSendReceiveAs(string format)
        {
            string exchange = GetType().Name;
            const string routingKeyPrefix = "Messages";

            IConnectionFactory connectionFactory = new ConnectionFactory { Uri = "amqp://localhost" };
            IMessageSource<IMessage> messageSource = CreateMessageSource(format, connectionFactory, exchange, routingKeyPrefix);
            IMessagePublisher<IMessage> messagePublisher = CreateMessagePublisher(format, connectionFactory, exchange, routingKeyPrefix);

            List<TestMessage> receivedMessages = new List<TestMessage>();
            messageSource.Messages
                         .OfType<TestMessage>()
                         .Subscribe(msg => { Console.WriteLine(msg); receivedMessages.Add(msg); }, Console.WriteLine, () => Console.WriteLine("Completed!"));

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

        private static IMessagePublisher<IMessage> CreateMessagePublisher(string format, IConnectionFactory connectionFactory,
            string exchange, string routingKeyPrefix)
        {
            IMessagePublisher<IMessage> messagePublisher = new MessagePublisher<IMessage>(connectionFactory,
                format == "Json" ? (IMessageSerializer) new JsonMessageSerializer() : new ProtoBufMessageSerializer(), exchange,
                routingKeyPrefix);
            return messagePublisher;
        }

        private static IMessageSource<IMessage> CreateMessageSource(string format, IConnectionFactory connectionFactory, string exchange,
            string routingKeyPrefix)
        {
            IMessageSource<IMessage> messageSource =
                new MessageSource<IMessage>(connectionFactory,
                    new List<IMessageDeserializer<TestMessage>>
                    {
                        format == "Json"
                            ? (IMessageDeserializer<TestMessage>) new JsonMessageDeserializer<TestMessage>()
                            : new ProtoBufMessageDeserializer<TestMessage>()
                    },
                    exchange, routingKeyPrefix);
            return messageSource;
        }
    }
}
