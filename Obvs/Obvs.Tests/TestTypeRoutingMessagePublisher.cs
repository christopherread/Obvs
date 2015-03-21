using System;
using System.Collections.Generic;
using FakeItEasy;
using NUnit.Framework;
using Obvs.Types;

namespace Obvs.Tests
{
    [TestFixture]
    public class TestTypeRoutingMessagePublisher
    {
        [Test]
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

            typeRoutingMessagePublisher.Publish(ev);
            typeRoutingMessagePublisher.Publish(command);
            typeRoutingMessagePublisher.Publish(message);

            A.CallTo(() => eventPublisher.Publish(ev)).MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => eventPublisher.Publish(message)).MustNotHaveHappened();
            A.CallTo(() => eventPublisher.Publish(command)).MustNotHaveHappened();

            A.CallTo(() => commandPublisher.Publish(ev)).MustNotHaveHappened();
            A.CallTo(() => commandPublisher.Publish(message)).MustNotHaveHappened();
            A.CallTo(() => commandPublisher.Publish(command)).MustHaveHappened(Repeated.Exactly.Once);

            A.CallTo(() => messagePublisher.Publish(ev)).MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => messagePublisher.Publish(command)).MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => messagePublisher.Publish(message)).MustHaveHappened(Repeated.Exactly.Once);
        }
    }
}