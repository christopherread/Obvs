﻿using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using Obvs.Serialization;
using Obvs.Serialization.Json;
using Obvs.Serialization.ProtoBuf;
using Obvs.Types;
using ProtoBuf;
using Xunit;

namespace Obvs.NetMQ.Tests
{
    public class TestPublishSubscribeInProc
    {
        [Fact, Trait("Category", "Explicit")]
        public async Task TestSendingAndReceivingStrings()
        {
            const string topic = "TestTopic";

            var messages = new ConcurrentBag<IMessage>();

            var observer = new AnonymousObserver<IMessage>(msg =>
            {
                Console.WriteLine("Received: " + msg);
                messages.Add(msg);

            }, err => Console.WriteLine("Error: " + err));


            IMessageSource<IMessage> source = new MessageSource<IMessage>("inproc://inproc-pubsub-a",
                new IMessageDeserializer<IMessage>[]
                {
                    new JsonMessageDeserializer<TestMessage1>(),
                    new JsonMessageDeserializer<TestMessage2>()
                },
                topic);

            IMessagePublisher<IMessage> publisher = new MessagePublisher<IMessage>("inproc://inproc-pubsub-a",
                new JsonMessageSerializer(), topic);

            var sub = source.Messages.Subscribe(observer);

            await publisher.PublishAsync(new TestMessage1 { Id = 1 });
            await publisher.PublishAsync(new TestMessage1 { Id = 2 });
            await publisher.PublishAsync(new TestMessage2 { Id = 1 });
            await publisher.PublishAsync(new TestMessage2 { Id = 2 });
            await publisher.PublishAsync(new TestMessage1 { Id = 3 });
            await publisher.PublishAsync(new TestMessage2 { Id = 3 });
            await Task.Delay(TimeSpan.FromSeconds(2));

            Assert.True(messages.OfType<TestMessage1>().Any(msg => msg.Id == 1), "TestMessage1 1 not received");
            Assert.True(messages.OfType<TestMessage1>().Any(msg => msg.Id == 2), "TestMessage1 2 not received");
            Assert.True(messages.OfType<TestMessage1>().Any(msg => msg.Id == 3), "TestMessage1 3 not received");

            Assert.True(messages.OfType<TestMessage2>().Any(msg => msg.Id == 1), "TestMessage2 1 not received");
            Assert.True(messages.OfType<TestMessage2>().Any(msg => msg.Id == 2), "TestMessage2 2 not received");
            Assert.True(messages.OfType<TestMessage2>().Any(msg => msg.Id == 3), "TestMessage2 3 not received");

            sub.Dispose();
            source.Dispose();
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task TestSendingAndReceivingBytes()
        {
            const string topic = "TestTopic";

            var messages = new ConcurrentBag<IMessage>();

            var observer = new AnonymousObserver<IMessage>(msg =>
            {
                Console.WriteLine("Received: " + msg);
                messages.Add(msg);

            }, err => Console.WriteLine("Error: " + err));

            IMessagePublisher<IMessage> publisher = new MessagePublisher<IMessage>("inproc://inproc-pubsub-b",
                new ProtoBufMessageSerializer(), topic);

            IMessageSource<IMessage> source = new MessageSource<IMessage>("inproc://inproc-pubsub-b",
                new IMessageDeserializer<IMessage>[]
                {
                    new ProtoBufMessageDeserializer<TestMessage1>(),
                    new ProtoBufMessageDeserializer<TestMessage2>()
                },
                topic);

            var sub = source.Messages.Subscribe(observer);

            await publisher.PublishAsync(new TestMessage1 { Id = 1 });
            await publisher.PublishAsync(new TestMessage1 { Id = 2 });
            await publisher.PublishAsync(new TestMessage2 { Id = 1 });
            await publisher.PublishAsync(new TestMessage2 { Id = 2 });
            await publisher.PublishAsync(new TestMessage1 { Id = 3 });
            await publisher.PublishAsync(new TestMessage2 { Id = 3 });
            await Task.Delay(TimeSpan.FromSeconds(3));

            Assert.True(messages.OfType<TestMessage1>().Any(msg => msg.Id == 1), "TestMessage1 1 not received");
            Assert.True(messages.OfType<TestMessage1>().Any(msg => msg.Id == 2), "TestMessage1 2 not received");
            Assert.True(messages.OfType<TestMessage1>().Any(msg => msg.Id == 3), "TestMessage1 3 not received");

            Assert.True(messages.OfType<TestMessage2>().Any(msg => msg.Id == 1), "TestMessage2 1 not received");
            Assert.True(messages.OfType<TestMessage2>().Any(msg => msg.Id == 2), "TestMessage2 2 not received");
            Assert.True(messages.OfType<TestMessage2>().Any(msg => msg.Id == 3), "TestMessage2 3 not received");

            sub.Dispose();
            source.Dispose();
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task TestMessagesLongerThan32Characters()
        {
            int max = 5;
            CountdownEvent cd = new CountdownEvent(max);

            const string topic = "TestTopicxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx";

            {
                var publisher = new MessagePublisher<IMessage>("inproc://inproc-pubsub-c",
                    new ProtoBufMessageSerializer(),
                    topic);

                for (int i = 0; i < max; i++)
                {
                    await publisher.PublishAsync(new TestMessageWhereTypeIsVeryMuchDefinitionLongerThen32Characters()
                    {
                        Id = i
                    });
                }
            }

            IDisposable sub;
            {
                var source = new MessageSource<IMessage>("inproc://inproc-pubsub-c",
                    new IMessageDeserializer<IMessage>[]
                    {
                        new ProtoBufMessageDeserializer<TestMessageWhereTypeIsVeryMuchDefinitionLongerThen32Characters>(),
                    },
                    topic);

                sub = source.Messages.Subscribe(msg =>
                   {
                       Console.WriteLine("Received: " + msg);
                       cd.Signal();
                   },
                   err => Console.WriteLine("Error: " + err));
            }


            await Task.Delay(TimeSpan.FromSeconds(2));

            sub.Dispose();
        }

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

        [ProtoContract]
        public class TestMessageWhereTypeIsVeryMuchDefinitionLongerThen32Characters : IMessage
        {
            [ProtoMember(1)]
            public int Id { get; set; }

            public override string ToString()
            {
                return "TestMessageWhereTypeIsVeryMuchDefinitionLongerThen32Characters-" + Id;
            }
        }
    }
}
