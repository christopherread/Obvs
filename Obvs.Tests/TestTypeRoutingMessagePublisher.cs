using System;
using System.Threading;
using System.Collections.Generic;
using FakeItEasy;
using Obvs.Types;
using Xunit;

namespace Obvs.Tests
{
    
    public class TestTypeRoutingMessagePublisher
    {
        [Fact]
        public void ShouldDispatchToCorrectPublisher()
        {
            IMessagePublisher<IMessage> eventPublisher = A.Fake<IMessagePublisher<IMessage>>();
            IMessagePublisher<IMessage> commandPublisher = A.Fake<IMessagePublisher<IMessage>>();
            IMessagePublisher<IMessage> messagePublisher = A.Fake<IMessagePublisher<IMessage>>();

            TypeRoutingMessagePublisher<IMessage> typeRoutingMessagePublisher =
                new TypeRoutingMessagePublisher<IMessage>(new[]
                {
                    new KeyValuePair<Type, IMessagePublisher<IMessage>>(typeof(IEvent), eventPublisher),
                    new KeyValuePair<Type, IMessagePublisher<IMessage>>(typeof(ICommand), commandPublisher),
                    new KeyValuePair<Type, IMessagePublisher<IMessage>>(typeof(IMessage), messagePublisher)
                });

            IEvent ev = A.Fake<IEvent>();
            ICommand command = A.Fake<ICommand>();
            IMessage message = A.Fake<IMessage>();

            typeRoutingMessagePublisher.PublishAsync(ev, TestContext.Current.CancellationToken);
            typeRoutingMessagePublisher.PublishAsync(command, TestContext.Current.CancellationToken);
            typeRoutingMessagePublisher.PublishAsync(message, TestContext.Current.CancellationToken);

            A.CallTo(() => eventPublisher.PublishAsync(ev, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
            A.CallTo(() => eventPublisher.PublishAsync(message, A<CancellationToken>._)).MustNotHaveHappened();
            A.CallTo(() => eventPublisher.PublishAsync(command, A<CancellationToken>._)).MustNotHaveHappened();

            A.CallTo(() => commandPublisher.PublishAsync(ev, A<CancellationToken>._)).MustNotHaveHappened();
            A.CallTo(() => commandPublisher.PublishAsync(message, A<CancellationToken>._)).MustNotHaveHappened();
            A.CallTo(() => commandPublisher.PublishAsync(command, A<CancellationToken>._)).MustHaveHappenedOnceExactly();

            A.CallTo(() => messagePublisher.PublishAsync(ev, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
            A.CallTo(() => messagePublisher.PublishAsync(command, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
            A.CallTo(() => messagePublisher.PublishAsync(message, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        }
    }
}