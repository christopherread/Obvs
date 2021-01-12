using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Obvs.RabbitMQ.Tests.Messages;
using Obvs.Serialization;
using Obvs.Serialization.Json;
using Obvs.Serialization.ProtoBuf;
using Obvs.Types;
using RabbitMQ.Client;
using Xunit;

namespace Obvs.RabbitMQ.Tests
{
    public class TestSendingAndReceiving
    {
        [Theory, Trait("Category", "Explicit")]
        [InlineData("Json")]
        [InlineData("ProtoBuf")]
        public async Task TestSendReceiveAs(string format)
        {
            var exchange = GetType().Name;
            const string routingKeyPrefix = "Messages";

            // edit to correspond with 5672 port on local RabbitMQ from DockerHub
            var connectionFactory = new ConnectionFactory
            {
                HostName = "192.168.99.100", 
                Port = 32769
            };
            var connection = new Lazy<IConnection>(() =>
            {
                var conn = connectionFactory.CreateConnection();
                return conn;
            }, LazyThreadSafetyMode.ExecutionAndPublication);

            var messageSource = CreateMessageSource(format, connection, exchange, routingKeyPrefix);
            var messagePublisher = CreateMessagePublisher(format, connection, exchange, routingKeyPrefix);

            var receivedMessages1 = new List<TestMessage>();
            var sub1 = messageSource.Messages
                         .OfType<TestMessage>()
                         .Subscribe(msg => { Console.WriteLine(msg); receivedMessages1.Add(msg); }, Console.WriteLine, () => Console.WriteLine("Completed!"));

            var receivedMessages2 = new List<TestMessage>();
            var sub2 = messageSource.Messages
                         .OfType<TestMessage>()
                         .Subscribe(msg => { Console.WriteLine(msg); receivedMessages2.Add(msg); }, Console.WriteLine, () => Console.WriteLine("Completed!"));

            var messages = new List<TestMessage>
            {
                new TestMessage {Data = "Hello"},
                new TestMessage {Data = "World!"},
                new TestMessage {Data = "..."},
                new TestMessage {Data = "Good"},
                new TestMessage {Data = "Bye"},
                new TestMessage {Data = "Cruel"},
                new TestMessage {Data = "World"}
            };

            foreach (var message in messages)
            {
                await messagePublisher.PublishAsync(message);
            }

            await Task.Delay(TimeSpan.FromSeconds(2));

            Assert.True(receivedMessages1.Count == 7, "Incorrect number of messages received for first subscription");
            Assert.True(receivedMessages2.Count == 7, "Incorrect number of messages received for second subscription");

            for (int index = 0; index < messages.Count; index++)
            {
                var message = messages[index];
                var received1 = receivedMessages1[index];
                var received2 = receivedMessages2[index];
                Assert.True(received1.Data == message.Data && received1.Timestamp == message.Timestamp, string.Format("Incorrect message1: {0}", received1));
                Assert.True(received2.Data == message.Data && received2.Timestamp == message.Timestamp, string.Format("Incorrect message2: {0}", received2));
            }

            sub1.Dispose();
            sub2.Dispose();
            messagePublisher.Dispose();
            messageSource.Dispose();
            connection.Value.Close();
        }

        private static IMessagePublisher<IMessage> CreateMessagePublisher(string format, Lazy<IConnection> connection,
            string exchange, string routingKeyPrefix)
        {
            return new MessagePublisher<IMessage>(connection,
                format == "Json"
                    ? (IMessageSerializer) new JsonMessageSerializer()
                    : new ProtoBufMessageSerializer(),
                exchange, routingKeyPrefix);
        }

        private static IMessageSource<IMessage> CreateMessageSource(string format, Lazy<IConnection> connection, string exchange,
            string routingKeyPrefix)
        {
            return new MessageSource<IMessage>(connection,
                new List<IMessageDeserializer<TestMessage>>
                {
                    format == "Json"
                        ? (IMessageDeserializer<TestMessage>) new JsonMessageDeserializer<TestMessage>()
                        : new ProtoBufMessageDeserializer<TestMessage>()
                },
                exchange, routingKeyPrefix);
        }
    }
}
