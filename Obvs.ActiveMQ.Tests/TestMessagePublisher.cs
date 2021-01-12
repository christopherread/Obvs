using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Concurrency;
using System.Threading.Tasks;
using Apache.NMS;
using Apache.NMS.ActiveMQ;
using Apache.NMS.ActiveMQ.Commands;
using FakeItEasy;
using Obvs.MessageProperties;
using Obvs.Serialization;
using Obvs.Types;
using Xunit;
using IMessage = Obvs.Types.IMessage;

namespace Obvs.ActiveMQ.Tests
{
    public class TestMessagePublisher
    {
        private IConnection _connection;
        private ISession _session;
        private IMessageProducer _producer;
        private IMessageSerializer _serializer;
        private IMessagePublisher<IMessage> _publisher;
        private IDestination _destination;
        private IBytesMessage _message;
        private Func<object, Dictionary<string, object>> _propertyProvider;
        private Lazy<IConnection> _lazyConnection;
        private readonly IScheduler _testScheduler = Scheduler.Immediate;

        private interface ITestMessage : IEvent
        {
        }

        private interface ITestMessage2 : IEvent
        {
        }

        private class TestMessage : ITestMessage
        {
            public int Id { get; set; }

            public override string ToString()
            {
                return "TestMessage " + Id;
            }
        }

        private class TestMessage2 : ITestMessage2
        {
            public int Id { get; set; }

            public override string ToString()
            {
                return "TestMessage2 " + Id;
            }
        }

        public TestMessagePublisher()
        {
        A.Fake<IConnectionFactory>();
            _connection = A.Fake<IConnection>();
            _lazyConnection = new Lazy<IConnection>(() =>
            {
                _connection.Start();
                return _connection;
            });
            _session = A.Fake<ISession>();
            _producer = A.Fake<IMessageProducer>();
            _serializer = A.Fake<IMessageSerializer>();
            _destination = A.Fake<IDestination>();
            _message = A.Fake<IBytesMessage>();

            A.CallTo(() => _connection.CreateSession(A<Apache.NMS.AcknowledgementMode>.Ignored)).Returns(_session);
            A.CallTo(() => _session.CreateProducer(_destination)).Returns(_producer);
            A.CallTo(() => _producer.CreateBytesMessage()).Returns(_message);
        }

        [Fact]
        public async Task ShouldCreateSessionsOnceOnFirstPublish()
        {
            _publisher = new MessagePublisher<IMessage>(_lazyConnection, _destination, _serializer, _propertyProvider, _testScheduler);

            await _publisher.PublishAsync(new TestMessage());

            A.CallTo(() => _session.CreateProducer(_destination)).MustHaveHappened(1, Times.Exactly);
            A.CallTo(() => _connection.Start()).MustHaveHappened(1, Times.Exactly);

            await _publisher.PublishAsync(new TestMessage());
            await _publisher.PublishAsync(new TestMessage());

            A.CallTo(() => _session.CreateProducer(_destination)).MustHaveHappened(1, Times.Exactly);
            A.CallTo(() => _connection.Start()).MustHaveHappened(1, Times.Exactly);
        }

        [Fact]
        public async Task ShouldSendMessageWithPropertiesWhenPublishing()
        {
            _propertyProvider = _ => new Dictionary<string, object>
            {
                {"key1", 1},
                {"key2", "2"},
                {"key3", 3.0},
                {"key4", 4L},
                {"key5", true}
            };

            _publisher = new MessagePublisher<IMessage>(_lazyConnection, _destination, _serializer, _propertyProvider, _testScheduler);

            ITestMessage testMessage = new TestMessage();
            

            await _publisher.PublishAsync(testMessage);

            A.CallTo(() => _producer.Send(_message, MsgDeliveryMode.NonPersistent, MsgPriority.Normal, TimeSpan.Zero)).MustHaveHappened(1, Times.Exactly);
            A.CallTo(() => _message.Properties.SetInt("key1", 1)).MustHaveHappened(1, Times.Exactly);
            A.CallTo(() => _message.Properties.SetString("key2", "2")).MustHaveHappened(1, Times.Exactly);
            A.CallTo(() => _message.Properties.SetDouble("key3", 3.0)).MustHaveHappened(1, Times.Exactly);
            A.CallTo(() => _message.Properties.SetLong("key4", 4L)).MustHaveHappened(1, Times.Exactly);
            A.CallTo(() => _message.Properties.SetBool("key5", true)).MustHaveHappened(1, Times.Exactly);
        }

        [Fact]
        public async Task ShouldSendBytesMessageSerializerReturnsBytes()
        {
            _publisher = new MessagePublisher<IMessage>(_lazyConnection, _destination, _serializer, _propertyProvider, _testScheduler);

            ITestMessage testMessage = new TestMessage();

            byte[] bytes = new byte[0];
            IBytesMessage bytesMessage = A.Fake<IBytesMessage>();
            A.CallTo(() => _serializer.Serialize(A<Stream>._, testMessage)).Invokes(arg => arg.Arguments.Get<Stream>(0).Write(bytes, 0, bytes.Length));
            A.CallTo(() => _producer.CreateBytesMessage()).Returns(bytesMessage);

            await _publisher.PublishAsync(testMessage);

            A.CallTo(() => _producer.Send(bytesMessage, MsgDeliveryMode.NonPersistent, MsgPriority.Normal, TimeSpan.Zero)).MustHaveHappened(1, Times.Exactly);
        }

        [Fact]
        public async Task ShouldSendMessageWithTypeNamePropertySet()
        {
            _publisher = new MessagePublisher<IMessage>(_lazyConnection, _destination, _serializer, _propertyProvider, _testScheduler);

            ITestMessage testMessage = new TestMessage();

            await _publisher.PublishAsync(testMessage);

            A.CallTo(() => _producer.Send(_message, MsgDeliveryMode.NonPersistent, MsgPriority.Normal, TimeSpan.Zero)).MustHaveHappened(1, Times.Exactly);
            A.CallTo(() => _message.Properties.SetString(MessagePropertyNames.TypeName, typeof(TestMessage).Name)).MustHaveHappened(1, Times.Exactly);
        }

        [Fact]
        public async Task ShouldCloseSessionWhenDisposed()
        {
            _publisher = new MessagePublisher<IMessage>(_lazyConnection, _destination, _serializer, _propertyProvider, _testScheduler);

            await _publisher.PublishAsync(new TestMessage());
            _publisher.Dispose();

            A.CallTo(() => _session.Close()).MustHaveHappened(1, Times.Exactly);
            A.CallTo(() => _producer.Close()).MustHaveHappened(1, Times.Exactly);
        }

        [Fact]
        public void ShouldThrowExceptionIfPublishAttemptedAfterDisposed()
        {
            _publisher = new MessagePublisher<IMessage>(_lazyConnection, _destination, _serializer, _propertyProvider, _testScheduler);
            
            Assert.ThrowsAsync<InvalidOperationException>(async () => 
            {    
                await _publisher.PublishAsync(new TestMessage());
                _publisher.Dispose();
                await _publisher.PublishAsync(new TestMessage());
            });
        }

        [Fact]
        public async Task ShouldThrowExceptionIfPropertyDictionaryAlreadyContainsTypeName()
        {
            Func<IMessage, Dictionary<string, object>> propertyProvider =
                msg => new Dictionary<string, object> {{MessagePropertyNames.TypeName, "Blah"}};

            _publisher = new MessagePublisher<IMessage>(_lazyConnection, _destination, _serializer, propertyProvider, _testScheduler);

            try
            {
                await _publisher.PublishAsync(new TestMessage());
            }
            catch (Exception exception)
            {
                Assert.Contains(MessagePropertyNames.TypeName, exception.Message);
            }
        }

        [Fact]
        public async Task ShouldDiscardMessagesThatAreStillQueuedOnSchedulerAfterDispose()
        {
            _publisher = new MessagePublisher<IMessage>(_lazyConnection, _destination, _serializer, _propertyProvider, _testScheduler);

            var message1 = new TestMessage();
            var message2 = new TestMessage();

            await _publisher.PublishAsync(message1);

            A.CallTo(() => _producer.Send(_message, MsgDeliveryMode.NonPersistent, MsgPriority.Normal, TimeSpan.Zero)).MustHaveHappened(1, Times.Exactly);

            await _publisher.PublishAsync(message2);

            _publisher.Dispose();

            A.CallTo(() => _producer.Send(_message, MsgDeliveryMode.NonPersistent, MsgPriority.Normal, TimeSpan.Zero)).MustHaveHappened(2, Times.Exactly);
        }

        [Fact]
        public async Task ShouldNotThrowExceptionIfPropertyProviderReturnsNull()
        {
            Func<IMessage, Dictionary<string, object>> propertyProviderWhichReturnsNull = msg => null;
            _publisher = new MessagePublisher<IMessage>(_lazyConnection, _destination, _serializer, propertyProviderWhichReturnsNull, _testScheduler);

            var message = new TestMessage();
            await _publisher.PublishAsync(message);

            A.CallTo(() => _producer.Send(_message, MsgDeliveryMode.NonPersistent, MsgPriority.Normal, TimeSpan.Zero)).MustHaveHappened(1, Times.Exactly);
        }

        //[Fact, Trait("Category", "Explicit")]
        //public async Task ShouldCorrectlyPublishAndSubscribeToMulipleMultiplexedTopics()
        //{
        //    const string brokerUri = "tcp://localhost:61616";
        //    const string topicName1 = "Obvs.Tests.ShouldCorrectlyPublishAndSubscribeToMulipleMultiplexedTopics1";
        //    const string topicName2 = "Obvs.Tests.ShouldCorrectlyPublishAndSubscribeToMulipleMultiplexedTopics2";

        //    Func<IMessage, Dictionary<string, object>> getProperties = message => new Dictionary<string, object>();

        //    IConnectionFactory connectionFactory = new ConnectionFactory(brokerUri);
        //    var lazyConnection = new Lazy<IConnection>(() =>
        //    {
        //        var conn = connectionFactory.CreateConnection();
        //        conn.Start();
        //        return conn;
        //    });

        //    IMessagePublisher<IMessage> publisher1 = new MessagePublisher<IMessage>(
        //        lazyConnection,
        //        new ActiveMQTopic(topicName1),
        //        new JsonMessageSerializer(),
        //        getProperties,
        //        _testScheduler);

        //    IMessagePublisher<IMessage> publisher2 = new MessagePublisher<IMessage>(
        //        lazyConnection,
        //        new ActiveMQTopic(topicName2),
        //        new JsonMessageSerializer(),
        //        getProperties,
        //        _testScheduler);

        //    IMessageDeserializer<IMessage>[] deserializers =
        //    {
        //        new JsonMessageDeserializer<TestMessage>(),
        //        new JsonMessageDeserializer<TestMessage2>()
        //    };

        //    IMessageSource<IMessage> source = new MergedMessageSource<IMessage>(new[]
        //    {
        //        new MessageSource<IMessage>(
        //            lazyConnection,
        //            deserializers,
        //            new ActiveMQTopic(topicName1)),

        //        new MessageSource<IMessage>(
        //            lazyConnection,
        //            deserializers,
        //            new ActiveMQTopic(topicName2))
        //    });

        //    source.Messages.Subscribe(Console.WriteLine);

        //    await publisher1.PublishAsync(new TestMessage { Id = 1234 });
        //    await publisher1.PublishAsync(new TestMessage2 { Id = 4567 });
        //    await publisher2.PublishAsync(new TestMessage { Id = 8910 });
        //    await publisher2.PublishAsync(new TestMessage2 { Id = 1112 });

        //    await Task.Delay(TimeSpan.FromSeconds(3));
        //}
    }
}
