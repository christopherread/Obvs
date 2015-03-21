using System;
using FakeItEasy;
using NUnit.Framework;
using Obvs.Types;

namespace Obvs.Tests
{
    [TestFixture]
    public class TestMessageBridge
    {
        public class TestMessage : IMessage { }

        [Test]
        public void ShouldSubscribeOnStart()
        {
            IMessageSource<TestMessage> source = A.Fake<IMessageSource<TestMessage>>();
            IMessageBridge bridge = new MessageBridge<TestMessage, TestMessage>(A.Fake<IMessagePublisher<TestMessage>>(), A.Fake<IMessageConverter<TestMessage, TestMessage>>(), source);

            bridge.Start();

            A.CallTo(() => source.Messages.Subscribe(A<IObserver<TestMessage>>.Ignored)).MustHaveHappened(Repeated.Exactly.Once);
        }

        [Test]
        public void ShouldUnsubscribeOnStop()
        {
            IMessageSource<TestMessage> source = A.Fake<IMessageSource<TestMessage>>();
            IMessageBridge bridge = new MessageBridge<TestMessage, TestMessage>(A.Fake<IMessagePublisher<TestMessage>>(), A.Fake<IMessageConverter<TestMessage, TestMessage>>(), source);
            IDisposable subscription = A.Fake<IDisposable>();

            A.CallTo(() => source.Messages.Subscribe(A<IObserver<TestMessage>>.Ignored)).Returns(subscription);

            bridge.Start();
            bridge.Stop();

            A.CallTo(() => subscription.Dispose()).MustHaveHappened(Repeated.Exactly.Once);
        }

        [Test]
        public void ShouldPublishTeamNewsWhenReceived()
        {
            IMessagePublisher<TestMessage> publisher = A.Fake<IMessagePublisher<TestMessage>>();
            IMessageSource<TestMessage> source = A.Fake<IMessageSource<TestMessage>>();
            IDisposable subscription = A.Fake<IDisposable>();
            IObserver<TestMessage> observer = null;
            TestMessage objFrom = new TestMessage();
            TestMessage objTo = new TestMessage();
            IMessageConverter<TestMessage, TestMessage> converter = A.Fake<IMessageConverter<TestMessage, TestMessage>>();
            IMessageBridge bridge = new MessageBridge<TestMessage, TestMessage>(publisher, converter, source);

            A.CallTo(() => converter.Convert(objFrom)).Returns(objTo);
            A.CallTo(() => source.Messages.Subscribe(A<IObserver<TestMessage>>.Ignored)).Returns(subscription);
            A.CallTo(() => source.Messages.Subscribe(A<IObserver<TestMessage>>.Ignored)).Invokes(call => observer = call.GetArgument<IObserver<TestMessage>>(0));

            bridge.Start();
            observer.OnNext(objFrom);

            A.CallTo(() => publisher.Publish(objTo)).MustHaveHappened(Repeated.Exactly.Once);
        }

        [Test]
        public void ShouldUnsubcribeOnDispose()
        {
            IMessagePublisher<TestMessage> publisher = A.Fake<IMessagePublisher<TestMessage>>();
            IMessageSource<TestMessage> source = A.Fake<IMessageSource<TestMessage>>();
            IMessageBridge bridge = new MessageBridge<TestMessage, TestMessage>(publisher, A.Fake<IMessageConverter<TestMessage, TestMessage>>(), source);
            IDisposable subscription = A.Fake<IDisposable>();

            A.CallTo(() => source.Messages.Subscribe(A<IObserver<TestMessage>>.Ignored)).Returns(subscription);

            bridge.Start();
            bridge.Dispose();

            A.CallTo(() => subscription.Dispose()).MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => publisher.Dispose()).MustHaveHappened(Repeated.Exactly.Once);
        }

    }
}