using System;
using System.Reactive.Concurrency;

using FakeItEasy;

using Obvs.Types;

using Xunit;

namespace Obvs.Tests {

    public class TestMessageSourceConverter {
        public class TestMessage : IMessage { }

        [Fact]
        public void ShouldSubscribeToUnderlyingSourceOnSubscribe() {
            IMessageSource<TestMessage> source = A.Fake<IMessageSource<TestMessage>>();
            IObservable<TestMessage> sourceMessagesObservable = A.Fake<IObservable<TestMessage>>();
            IMessageSource<TestMessage> sourceConverter = new MessageSourceConverter<TestMessage, TestMessage>(source, A.Fake<IMessageConverter<TestMessage, TestMessage>>());
            IScheduler scheduler = A.Fake<IScheduler>();
            IObserver<TestMessage> consumer = A.Fake<IObserver<TestMessage>>();

            A.CallTo(() => source.GetMessages(A<IScheduler>._)).Returns(sourceMessagesObservable);
            sourceConverter.GetMessages(scheduler).Subscribe(consumer);

            A.CallTo(() => source.GetMessages(scheduler).Subscribe(A<IObserver<TestMessage>>.Ignored))
                .MustHaveHappened(Repeated.Exactly.Once);
        }

        [Fact]
        public void ShouldConvertAndPublishMessages() {
            var source = A.Fake<IMessageSource<TestMessage>>();
            var sourceMessagesObservable = A.Fake<IObservable<TestMessage>>();
            var converter = A.Fake<IMessageConverter<TestMessage, TestMessage>>();
            var sourceConverter = new MessageSourceConverter<TestMessage, TestMessage>(source, converter);
            IObserver<TestMessage> internalObserver = null;
            var consumer = A.Fake<IObserver<TestMessage>>();
            var scheduler = A.Fake<IScheduler>();
            var message = new TestMessage();
            var convertedMessage = new TestMessage();

            A.CallTo(() => source.GetMessages(A<IScheduler>._)).Returns(sourceMessagesObservable);
            A.CallTo(() => source.GetMessages(scheduler).Subscribe(A<IObserver<TestMessage>>.Ignored))
                .Invokes(call => internalObserver = call.GetArgument<IObserver<TestMessage>>(0));
            A.CallTo(() => converter.Convert(message)).Returns(convertedMessage);

            sourceConverter.Messages.Subscribe(consumer);

            internalObserver.OnNext(message);

            A.CallTo(() => converter.Convert(message)).MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => consumer.OnNext(convertedMessage)).MustHaveHappened(Repeated.Exactly.Once);
        }

        [Fact]
        public void ShouldNotPublishInvalidMessages() {
            IMessageSource<TestMessage> source = A.Fake<IMessageSource<TestMessage>>();
            IObservable<TestMessage> sourceMessagesObservable = A.Fake<IObservable<TestMessage>>();
            IMessageConverter<TestMessage, TestMessage> converter = A.Fake<IMessageConverter<TestMessage, TestMessage>>();
            IMessageSource<TestMessage> sourceConverter = new MessageSourceConverter<TestMessage, TestMessage>(source, converter);
            IObserver<TestMessage> internalObserver = null;
            IObserver<TestMessage> consumer = A.Fake<IObserver<TestMessage>>();
            IScheduler scheduler = A.Fake<IScheduler>();
            TestMessage message = new TestMessage();
            
            A.CallTo(() => source.GetMessages(A<IScheduler>._)).Returns(sourceMessagesObservable);
            A.CallTo(() => source.GetMessages(scheduler).Subscribe(A<IObserver<TestMessage>>.Ignored))
                .Invokes(call => internalObserver = call.GetArgument<IObserver<TestMessage>>(0));
            A.CallTo(() => converter.Convert(message)).Returns(null);

            sourceConverter.Messages.Subscribe(consumer);

            internalObserver.OnNext(message);

            A.CallTo(() => converter.Convert(message)).MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => consumer.OnNext(A<TestMessage>.Ignored)).MustNotHaveHappened();
        }
    }
}