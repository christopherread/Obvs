using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reactive;
using System.Threading;
using NetMQ;
using NUnit.Framework;
using Obvs.Serialization;
using Obvs.Types;
using ProtoBuf;

namespace Obvs.NetMQ.Tests
{
    [TestFixture]
    public class TestPublishSubscribe
    {
        [ProtoContract]
        public class TestMessage1 : IMessage
        {
             [ProtoMember(1)]
            public int Id { get; set; }

            public override string ToString()
            {
                return "TestMessage1-" + Id;
            }
        }
        
        [ProtoContract]
        public class TestMessage2 : IMessage
        {
            [ProtoMember(1)]
            public int Id { get; set; }

            public override string ToString()
            {
                return "TestMessage2-" + Id;
            }
        }

        [Test, Explicit]
        public void TestSendingAndReceivingStringsOverLocalHost()
        {
            const string topic = "TestTopic";

            ConcurrentBag<IMessage> messages = new ConcurrentBag<IMessage>();

            AnonymousObserver<IMessage> observer = new AnonymousObserver<IMessage>(msg =>
            {
                Console.WriteLine("Received: " + msg);
                messages.Add(msg);

            }, err => Console.WriteLine("Error: " + err));

            NetMQContext context = NetMQContext.Create();

            IMessageSource<IMessage> source = new MessageSource<IMessage>("tcp://localhost:5556",
                new IMessageDeserializer<IMessage>[]
                {
                    new JsonMessageDeserializer<TestMessage1>(), 
                    new JsonMessageDeserializer<TestMessage2>(),
                },
                context, topic);

            IDisposable sub = source.Messages.Subscribe(observer);

            IMessagePublisher<IMessage> publisher = new MessagePublisher<IMessage>("tcp://localhost:5556",
                new JsonMessageSerializer(), context, topic);

            publisher.Publish(new TestMessage1 { Id = 1 });
            publisher.Publish(new TestMessage1 { Id = 2 });
            publisher.Publish(new TestMessage2 { Id = 1 });
            publisher.Publish(new TestMessage2 { Id = 2 });
            publisher.Publish(new TestMessage1 { Id = 3 });
            publisher.Publish(new TestMessage2 { Id = 3 });

            Thread.Sleep(TimeSpan.FromSeconds(3));

            Assert.That(messages.OfType<TestMessage1>().Any(msg => msg.Id == 1), "TestMessage1 1 not received");
            Assert.That(messages.OfType<TestMessage1>().Any(msg => msg.Id == 2), "TestMessage1 2 not received");
            Assert.That(messages.OfType<TestMessage1>().Any(msg => msg.Id == 3), "TestMessage1 3 not received");

            Assert.That(messages.OfType<TestMessage2>().Any(msg => msg.Id == 1), "TestMessage2 1 not received");
            Assert.That(messages.OfType<TestMessage2>().Any(msg => msg.Id == 2), "TestMessage2 2 not received");
            Assert.That(messages.OfType<TestMessage2>().Any(msg => msg.Id == 3), "TestMessage2 3 not received");

            sub.Dispose();
            source.Dispose();
        }

        [Test, Explicit]
        public void TestSendingAndReceivingBytesOverLocalHost()
        {
            const string topic = "TestTopic";

            ConcurrentBag<IMessage> messages = new ConcurrentBag<IMessage>();

            AnonymousObserver<IMessage> observer = new AnonymousObserver<IMessage>(msg =>
            {
                Console.WriteLine("Received: " + msg);
                messages.Add(msg);

            }, err => Console.WriteLine("Error: " + err));

            NetMQContext context = NetMQContext.Create();

            IMessageSource<IMessage> source = new MessageSource<IMessage>("tcp://localhost:5556",
                new IMessageDeserializer<IMessage>[]
                {
                    new ProtoBufMessageDeserializer<TestMessage1>(), 
                    new ProtoBufMessageDeserializer<TestMessage2>(),
                },
                context, topic);
            
            IDisposable sub = source.Messages.Subscribe(observer);

            IMessagePublisher<IMessage> publisher = new MessagePublisher<IMessage>("tcp://localhost:5556",
                new ProtoBufMessageSerializer(), context, topic);

            publisher.Publish(new TestMessage1 { Id = 1 });
            publisher.Publish(new TestMessage1 { Id = 2 });
            publisher.Publish(new TestMessage2 { Id = 1 });
            publisher.Publish(new TestMessage2 { Id = 2 });
            publisher.Publish(new TestMessage1 { Id = 3 });
            publisher.Publish(new TestMessage2 { Id = 3 });

            Thread.Sleep(TimeSpan.FromSeconds(3));

            Assert.That(messages.OfType<TestMessage1>().Any(msg => msg.Id == 1), "TestMessage1 1 not received");
            Assert.That(messages.OfType<TestMessage1>().Any(msg => msg.Id == 2), "TestMessage1 2 not received");
            Assert.That(messages.OfType<TestMessage1>().Any(msg => msg.Id == 3), "TestMessage1 3 not received");

            Assert.That(messages.OfType<TestMessage2>().Any(msg => msg.Id == 1), "TestMessage2 1 not received");
            Assert.That(messages.OfType<TestMessage2>().Any(msg => msg.Id == 2), "TestMessage2 2 not received");
            Assert.That(messages.OfType<TestMessage2>().Any(msg => msg.Id == 3), "TestMessage2 3 not received");

            sub.Dispose();
            source.Dispose();
        }
    }
}
