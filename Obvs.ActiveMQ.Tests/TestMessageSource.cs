using System;
using System.Collections;
using System.IO;
using System.Text;
using System.Xml.Serialization;
using Apache.NMS;
using Apache.NMS.Util;
using FakeItEasy;
using Moq;
using NUnit.Framework;
using Obvs.MessageProperties;
using Obvs.Serialization;
using IMessage = Obvs.Types.IMessage;

namespace Obvs.ActiveMQ.Tests
{
    [TestFixture]
    public class TestMessageSource
    {
        private IConnection _connection;
        private ISession _session;
        private IMessageConsumer _consumer;
        private IMessageDeserializer<ITestMessage> _deserializer;
        private IObserver<ITestMessage> _observer;
        private IMessageSource<ITestMessage> _source;
        private IDestination _destination;
        private AcknowledgementMode _acknowledgementMode;
        private Lazy<IConnection> _lazyConnection;

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
            A.Fake<IConnectionFactory>();
            _connection = A.Fake<IConnection>();
            _lazyConnection = new Lazy<IConnection>(() =>
            {
                _connection.Start();
                return _connection;
            });
            _session = A.Fake<ISession>();
            _consumer = A.Fake<IMessageConsumer>();
            _deserializer = A.Fake<IMessageDeserializer<ITestMessage>>();
            _observer = A.Fake<IObserver<ITestMessage>>();
            _destination = A.Fake<IDestination>();
            _acknowledgementMode = AcknowledgementMode.AutoAcknowledge;

            A.CallTo(() => _connection.CreateSession(A<Apache.NMS.AcknowledgementMode>.Ignored)).Returns(_session);
            A.CallTo(() => _session.CreateConsumer(_destination)).Returns(_consumer);

            _source = new MessageSource<ITestMessage>(_lazyConnection, new[] {_deserializer}, _destination,
                _acknowledgementMode);
        }

        [Test]
        public void ShouldConnectToBrokerWhenSubscribedTo()
        {
            _source.Messages.Subscribe(_observer);

            A.CallTo(() => _connection.Start()).MustHaveHappened(Repeated.Exactly.Once);
        }

        [Test]
        public void ShouldStartListeningToMessagesWhenSubscribedTo()
        {
            _source.Messages.Subscribe(_observer);

            A.CallTo(() => _connection.CreateSession(_acknowledgementMode == AcknowledgementMode.ClientAcknowledge ? Apache.NMS.AcknowledgementMode.ClientAcknowledge : Apache.NMS.AcknowledgementMode.AutoAcknowledge)).MustHaveHappened(Repeated.Exactly.Once);
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
        public void ShouldNotAcknowledgeMessagesIfInvalid()
        {
            _source = new MessageSource<ITestMessage>(_lazyConnection, new[] {_deserializer}, _destination,
                AcknowledgementMode.ClientAcknowledge);

            Mock<IMessageConsumer> mockConsumer = MockConsumerExtensions.Create(_session, _destination);
            IBytesMessage bytesMessage = A.Fake<IBytesMessage>();
            A.CallTo(() => _deserializer.Deserialize(A<Stream>.Ignored)).Throws<Exception>();

            _source.Messages.Subscribe(_observer);
            mockConsumer.RaiseFakeMessage(bytesMessage);

            A.CallTo(() => bytesMessage.Acknowledge()).MustNotHaveHappened();
        }

        [Test]
        public void ShouldAcknowledgeMessagesIfValid()
        {
            _source = new MessageSource<ITestMessage>(_lazyConnection, new[] {_deserializer}, _destination,
                AcknowledgementMode.ClientAcknowledge);

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
            IBytesMessage bytesMessage = A.Fake<IBytesMessage>();
            ITestMessage message = A.Fake<ITestMessage>();

            const string serializedFixtureString = "<xml>Some fixture XML</xml>";
            var bytes = Encoding.UTF8.GetBytes(serializedFixtureString);

            A.CallTo(() => bytesMessage.Content).Returns(bytes);
            A.CallTo(() => _deserializer.Deserialize(A<Stream>._)).Returns(message);

            _source.Messages.Subscribe(_observer);
            mockConsumer.RaiseFakeMessage(bytesMessage);

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
            A.CallTo(() => _deserializer.Deserialize(A<Stream>._)).Returns(message);

            _source.Messages.Subscribe(_observer);
            mockConsumer.RaiseFakeMessage(bytesMessage);

            A.CallTo(() => _observer.OnNext(message)).MustHaveHappened();
        }

        [Test]
        public void ShouldDeserializeAndPublishMessageOfRightTypeName()
        {
            Mock<IMessageConsumer> mockConsumer = MockConsumerExtensions.Create(_session, _destination);
            IBytesMessage bytesMessage = A.Fake<IBytesMessage>();
            ITestMessage message = A.Fake<ITestMessage>();

            const string serializedFixtureString = "<xml>Some fixture XML</xml>";
            var bytes = Encoding.UTF8.GetBytes(serializedFixtureString);

            A.CallTo(() => bytesMessage.Content).Returns(bytes);
            A.CallTo(() => bytesMessage.Properties.Contains(MessagePropertyNames.TypeName)).Returns(true);
            A.CallTo(() => bytesMessage.Properties.GetString(MessagePropertyNames.TypeName)).Returns("SomeTypeName");
            A.CallTo(() => _deserializer.Deserialize(A<Stream>._)).Returns(message);
            A.CallTo(() => _deserializer.GetTypeName()).Returns("SomeTypeName");

            _source = new MessageSource<ITestMessage>(_lazyConnection, new[] {_deserializer}, _destination,
                _acknowledgementMode);

            _source.Messages.Subscribe(_observer);
            mockConsumer.RaiseFakeMessage(bytesMessage);

            A.CallTo(() => _observer.OnNext(message)).MustHaveHappened();
        }

        [Test]
        public void ShouldProcessMessagesWithoutTypeNameIfOnlyOneDeserializerSupplied()
        {
            Mock<IMessageConsumer> mockConsumer = MockConsumerExtensions.Create(_session, _destination);
            IBytesMessage bytesMessage = A.Fake<IBytesMessage>();
            ITestMessage message = A.Fake<ITestMessage>();
            
            const string serializedFixtureString = "<xml>Some fixture XML</xml>";
            var bytes = Encoding.UTF8.GetBytes(serializedFixtureString);
            
            A.CallTo(() => bytesMessage.Content).Returns(bytes);
            A.CallTo(() => bytesMessage.Properties).Returns(new PrimitiveMap());
            A.CallTo(() => _deserializer.Deserialize(A<Stream>._)).Returns(message);
            A.CallTo(() => _deserializer.GetTypeName()).Returns("");

            _source = new MessageSource<ITestMessage>(_lazyConnection, new[] {_deserializer}, _destination, _acknowledgementMode);

            _source.Messages.Subscribe(_observer);
            mockConsumer.RaiseFakeMessage(bytesMessage);

            A.CallTo(() => _deserializer.Deserialize(A<Stream>._)).MustHaveHappened();
            A.CallTo(() => _observer.OnNext(message)).MustHaveHappened();
        }

        [Test]
        public void ShouldProcessMessagesAccordingToFilter()
        {
            const string typeName = "SomeTypeName";

            Mock<IMessageConsumer> mockConsumer = MockConsumerExtensions.Create(_session, _destination);
            IBytesMessage bytesMessage = A.Fake<IBytesMessage>();
            ITestMessage message = A.Fake<ITestMessage>();
            Func<IDictionary, bool> filter = properties => (int?) properties["Id"] == 123;
            
            const string serializedFixtureString = "<xml>Some fixture XML</xml>";
            var bytes = Encoding.UTF8.GetBytes(serializedFixtureString);

            var messageProperties = new PrimitiveMap();
            messageProperties.SetString(MessagePropertyNames.TypeName, typeName);
            messageProperties.SetInt("Id", 123);
            
            A.CallTo(() => bytesMessage.Content).Returns(bytes);
            A.CallTo(() => bytesMessage.Properties).Returns(messageProperties);
            A.CallTo(() => _deserializer.Deserialize(A<Stream>._)).Returns(message);
            A.CallTo(() => _deserializer.GetTypeName()).Returns(typeName);

            _source = new MessageSource<ITestMessage>(_lazyConnection, new[] {_deserializer}, _destination,
                _acknowledgementMode, null, filter);

            _source.Messages.Subscribe(_observer);
            mockConsumer.RaiseFakeMessage(bytesMessage);

            A.CallTo(() => _deserializer.Deserialize(A<Stream>._)).MustHaveHappened();
            A.CallTo(() => _observer.OnNext(message)).MustHaveHappened();
        }
    }
}