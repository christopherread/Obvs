using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Obvs.Serialization.Json;
using Obvs.Types;
using RabbitMQ.Client;

namespace Obvs.RabbitMQ.Tests
{
    [TestFixture]
    public class TestMessageSource
    {
        public class TestMessage : IMessage
        {
            public DateTime Timestamp { get; set; }
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
        public void TestSendReceive()
        {
            IConnectionFactory connectionFactory = new ConnectionFactory {HostName = "localhost"};
            string exchange = GetType().Name;

            IMessageSource<IMessage> messageSource =
                new MessageSource<IMessage>(connectionFactory,
                    new List<IMessageDeserializer<TestMessage>> {new JsonMessageDeserializer<TestMessage>()},
                    exchange);

            IMessagePublisher<IMessage> messagePublisher = new MessagePublisher<IMessage>(connectionFactory,
                new JsonMessageSerializer(), exchange);

            messageSource.Messages.Subscribe(Console.WriteLine, Console.WriteLine, () => Console.WriteLine("Completed!"));

            messagePublisher.Publish(new TestMessage {Data = "Hello"});
            messagePublisher.Publish(new TestMessage {Data = "World"});
            messagePublisher.Publish(new TestMessage {Data = "Bye!"});

            Thread.Sleep(TimeSpan.FromSeconds(10));
        }
    }
}
