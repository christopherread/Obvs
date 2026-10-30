using System;
using System.Threading;
using System.Collections.Generic;
using FakeItEasy;
using Obvs.Transports;
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

            typeRoutingMessagePublisher.PublishAsync(ev, new Dictionary<string, string>(), TestContext.Current.CancellationToken);
            typeRoutingMessagePublisher.PublishAsync(command, new Dictionary<string, string>(), TestContext.Current.CancellationToken);
            typeRoutingMessagePublisher.PublishAsync(message, new Dictionary<string, string>(), TestContext.Current.CancellationToken);

            A.CallTo(() => eventPublisher.PublishAsync(ev, A<IReadOnlyDictionary<string, string>>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
            A.CallTo(() => eventPublisher.PublishAsync(message, A<IReadOnlyDictionary<string, string>>._, A<CancellationToken>._)).MustNotHaveHappened();
            A.CallTo(() => eventPublisher.PublishAsync(command, A<IReadOnlyDictionary<string, string>>._, A<CancellationToken>._)).MustNotHaveHappened();

            A.CallTo(() => commandPublisher.PublishAsync(ev, A<IReadOnlyDictionary<string, string>>._, A<CancellationToken>._)).MustNotHaveHappened();
            A.CallTo(() => commandPublisher.PublishAsync(message, A<IReadOnlyDictionary<string, string>>._, A<CancellationToken>._)).MustNotHaveHappened();
            A.CallTo(() => commandPublisher.PublishAsync(command, A<IReadOnlyDictionary<string, string>>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();

            A.CallTo(() => messagePublisher.PublishAsync(ev, A<IReadOnlyDictionary<string, string>>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
            A.CallTo(() => messagePublisher.PublishAsync(command, A<IReadOnlyDictionary<string, string>>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
            A.CallTo(() => messagePublisher.PublishAsync(message, A<IReadOnlyDictionary<string, string>>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        }
    }
}