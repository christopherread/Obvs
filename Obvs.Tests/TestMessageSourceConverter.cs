using System;
using FakeItEasy;
using Obvs.Types;
using Xunit;

namespace Obvs.Tests
{
    
    public class TestMessageSourceConverter
    {
        public class TestMessage : IMessage { }

        [Fact]
        public void ShouldSubscribeToUnderlyingSourceOnSubscribe()
        {
            IMessageSource<TestMessage> source = A.Fake<IMessageSource<TestMessage>>();
            IMessageSource<TestMessage> sourceConverter = new MessageSourceConverter<TestMessage, TestMessage>(source, A.Fake<IMessageConverter<TestMessage, TestMessage>>());
            IObserver<TestMessage> consumer = A.Fake<IObserver<TestMessage>>();

            sourceConverter.Messages.Subscribe(consumer);

            A.CallTo(() => source.Messages.Subscribe(A<IObserver<TestMessage>>.Ignored)).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public void ShouldConvertAndPublishMessages()
        {
            IMessageSource<TestMessage> source = A.Fake<IMessageSource<TestMessage>>();
            IMessageConverter<TestMessage, TestMessage> converter = A.Fake<IMessageConverter<TestMessage, TestMessage>>();
            IMessageSource<TestMessage> sourceConverter = new MessageSourceConverter<TestMessage, TestMessage>(source, converter);
            IObserver<TestMessage> internalObserver = null;
            IObserver<TestMessage> consumer = A.Fake<IObserver<TestMessage>>();
            TestMessage message = new TestMessage();
            TestMessage convertedMessage = new TestMessage();

            A.CallTo(() => source.Messages.Subscribe(A<IObserver<TestMessage>>.Ignored)).Invokes(call => internalObserver = call.GetArgument<IObserver<TestMessage>>(0));
            A.CallTo(() => converter.Convert(message)).Returns(convertedMessage);

            sourceConverter.Messages.Subscribe(consumer);

            internalObserver.OnNext(message);

            A.CallTo(() => converter.Convert(message)).MustHaveHappenedOnceExactly();
            A.CallTo(() => consumer.OnNext(convertedMessage)).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public void ShouldNotPublishInvalidMessages()
        {
            IMessageSource<TestMessage> source = A.Fake<IMessageSource<TestMessage>>();
            IMessageConverter<TestMessage, TestMessage> converter = A.Fake<IMessageConverter<TestMessage, TestMessage>>();
            IMessageSource<TestMessage> sourceConverter = new MessageSourceConverter<TestMessage, TestMessage>(source, converter);
            IObserver<TestMessage> internalObserver = null;
            IObserver<TestMessage> consumer = A.Fake<IObserver<TestMessage>>();
            TestMessage message = new TestMessage();

            A.CallTo(() => source.Messages.Subscribe(A<IObserver<TestMessage>>.Ignored)).Invokes(call => internalObserver = call.GetArgument<IObserver<TestMessage>>(0));
            A.CallTo(() => converter.Convert(message)).Returns(null);

            sourceConverter.Messages.Subscribe(consumer);

            internalObserver.OnNext(message);

            A.CallTo(() => converter.Convert(message)).MustHaveHappenedOnceExactly();
            A.CallTo(() => consumer.OnNext(A<TestMessage>.Ignored)).MustNotHaveHappened();
        }
    }
}