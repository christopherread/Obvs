using System;
using System.Xml.Serialization;
using Apache.NMS;
using FakeItEasy;
using Moq;
using NUnit.Framework;
using IMessage = Obvs.Types.IMessage;

namespace Obvs.ActiveMq.Tests
{
    [TestFixture]
    public class TestMessageSource
    {
        private IConnectionFactory _connectionFactory;
        private IConnection _connection;
        private ISession _session;
        private IMessageConsumer _consumer;
        private IMessageDeserializer<ITestMessage> _deserializer;
        private IObserver<ITestMessage> _observer;
        private IMessageSource<ITestMessage> _source;
        private IDestination _destination;
        private AcknowledgementMode _acknowledgementMode;

        public interface ITestMessage : IMessage
        {
        }

        [XmlRoot]
        public class TestMessage : ITestMessage
        {
        }

        [SetUp]
        public void SetUp()
        {
            _connectionFactory = A.Fake<IConnectionFactory>();
            _connection = A.Fake<IConnection>();
            _session = A.Fake<ISession>();
            _consumer = A.Fake<IMessageConsumer>();
            _deserializer = A.Fake<IMessageDeserializer<ITestMessage>>();
            _observer = A.Fake<IObserver<ITestMessage>>();
            _destination = A.Fake<IDestination>();
            _acknowledgementMode = AcknowledgementMode.AutoAcknowledge;

            A.CallTo(() => _connectionFactory.CreateConnection()).Returns(_connection);
            A.CallTo(() => _connection.CreateSession(A<AcknowledgementMode>.Ignored)).Returns(_session);
            A.CallTo(() => _session.CreateConsumer(_destination)).Returns(_consumer);

            _source = new MessageSource<ITestMessage>(_connectionFactory, new[] {_deserializer}, _destination,
                _acknowledgementMode);
        }

        [Test]
        public void ShouldConnectToBrokerWhenSubscribedTo()
        {
            _source.Messages.Subscribe(_observer);

            A.CallTo(() => _connectionFactory.CreateConnection()).MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => _connection.Start()).MustHaveHappened(Repeated.Exactly.Once);
        }

        [Test]
        public void ShouldStartListeningToMessagesWhenSubscribedTo()
        {
            _source.Messages.Subscribe(_observer);

            A.CallTo(() => _connection.CreateSession(_acknowledgementMode)).MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => _session.CreateConsumer(_destination)).MustHaveHappened(Repeated.Exactly.Once);

            A.CallTo(_consumer).Where(x => x.Method.Name.Equals("add_Listener")).MustHaveHappened();
        }

        [Test]
        public void ShouldCloseSessionWhenSubscriptionDisposed()
        {
            IDisposable subscription = _source.Messages.Subscribe(_observer);
            subscription.Dispose();

            A.CallTo(() => _consumer.Close()).MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => _consumer.Dispose()).MustHaveHappened(Repeated.Exactly.Once);

            A.CallTo(() => _session.Close()).MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => _session.Dispose()).MustHaveHappened(Repeated.Exactly.Once);
        }

        [Test]
        public void ShouldDisconnectFromBrokerWhenDisposed()
        {
            _source.Messages.Subscribe(_observer);

            _source.Dispose();

            A.CallTo(() => _connection.Stop()).MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => _connection.Close()).MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => _connection.Dispose()).MustHaveHappened(Repeated.Exactly.Once);
        }

        [Test]
        public void ShouldNotConnectToBrokerMoreThanOnce()
        {
            IDisposable subscription1 = _source.Messages.Subscribe(_observer);
            IDisposable subscription2 = _source.Messages.Subscribe(_observer);
            IDisposable subscription3 = _source.Messages.Subscribe(_observer);

            A.CallTo(() => _connectionFactory.CreateConnection()).MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => _connection.Start()).MustHaveHappened(Repeated.Exactly.Once);

            subscription1.Dispose();
            subscription2.Dispose();
            subscription3.Dispose();
        }

        [Test]
        public void ShouldNotAcknowledgeMessagesIfInvalid()
        {
            _source = new MessageSource<ITestMessage>(_connectionFactory, new[] {_deserializer}, _destination,
                AcknowledgementMode.IndividualAcknowledge);

            Mock<IMessageConsumer> mockConsumer = MockConsumerExtensions.Create(_session, _destination);
            ITextMessage textMessage = A.Fake<ITextMessage>();
            A.CallTo(() => _deserializer.Deserialize(A<string>.Ignored)).Throws<Exception>();

            _source.Messages.Subscribe(_observer);
            mockConsumer.RaiseFakeMessage(textMessage);

            A.CallTo(() => textMessage.Acknowledge()).MustNotHaveHappened();
        }

        [Test]
        public void ShouldAcknowledgeMessagesIfValid()
        {
            _source = new MessageSource<ITestMessage>(_connectionFactory, new[] {_deserializer}, _destination,
                AcknowledgementMode.IndividualAcknowledge);

            Mock<IMessageConsumer> mockConsumer = MockConsumerExtensions.Create(_session, _destination);
            ITextMessage textMessage = A.Fake<ITextMessage>();

            _source.Messages.Subscribe(_observer);
            mockConsumer.RaiseFakeMessage(textMessage);

            A.CallTo(() => textMessage.Acknowledge()).MustHaveHappened(Repeated.Exactly.Once);
        }

        [Test]
        public void ShouldDeserializeAndPublishMessageWhenReceived()
        {
            Mock<IMessageConsumer> mockConsumer = MockConsumerExtensions.Create(_session, _destination);
            ITextMessage textMessage = A.Fake<ITextMessage>();
            ITestMessage message = A.Fake<ITestMessage>();
            const string serializedFixtureString = "<xml>Some fixture XML</xml>";
            A.CallTo(() => textMessage.Text).Returns(serializedFixtureString);
            A.CallTo(() => _deserializer.Deserialize(serializedFixtureString)).Returns(message);

            _source.Messages.Subscribe(_observer);
            mockConsumer.RaiseFakeMessage(textMessage);

            A.CallTo(() => _observer.OnNext(message)).MustHaveHappened();
        }

        [Test]
        public void ShouldDeserializeByteMessagesAndPublishMessageWhenReceived()
        {
            Mock<IMessageConsumer> mockConsumer = MockConsumerExtensions.Create(_session, _destination);
            IBytesMessage bytesMessage = A.Fake<IBytesMessage>();
            ITestMessage message = A.Fake<ITestMessage>();
            byte[] bytes = new byte[0];
            A.CallTo(() => bytesMessage.Content).Returns(bytes);
            A.CallTo(() => _deserializer.Deserialize(bytes)).Returns(message);

            _source.Messages.Subscribe(_observer);
            mockConsumer.RaiseFakeMessage(bytesMessage);

            A.CallTo(() => _observer.OnNext(message)).MustHaveHappened();
        }

        [Test]
        public void ShouldDeserializeAndPublishMessageOfRightTypeName()
        {
            Mock<IMessageConsumer> mockConsumer = MockConsumerExtensions.Create(_session, _destination);
            ITextMessage textMessage = A.Fake<ITextMessage>();
            ITestMessage message = A.Fake<ITestMessage>();
            const string serializedFixtureString = "<xml>Some fixture XML</xml>";

            A.CallTo(() => textMessage.Text).Returns(serializedFixtureString);
            A.CallTo(() => textMessage.Properties.Contains(MessagePropertyNames.TypeName)).Returns(true);
            A.CallTo(() => textMessage.Properties.GetString(MessagePropertyNames.TypeName)).Returns("SomeTypeName");
            A.CallTo(() => _deserializer.Deserialize(serializedFixtureString)).Returns(message);
            A.CallTo(() => _deserializer.GetTypeName()).Returns("SomeTypeName");

            _source = new MessageSource<ITestMessage>(_connectionFactory, new[] {_deserializer}, _destination,
                _acknowledgementMode);

            _source.Messages.Subscribe(_observer);
            mockConsumer.RaiseFakeMessage(textMessage);

            A.CallTo(() => _observer.OnNext(message)).MustHaveHappened();
        }
    }
}