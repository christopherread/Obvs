using System;
using System.Reactive.Subjects;
using Obvs.Types;

namespace Obvs
{
    public interface IMessageBus<TMessage> : IMessagePublisher<TMessage>, IMessageSource<TMessage> 
        where TMessage : IMessage
    {
    }

    public class SubjectMessageBus<TMessage> : IMessageBus<TMessage>
        where TMessage : IMessage
    {
        private readonly Subject<TMessage> _subject;

        public SubjectMessageBus()
        {
            _subject = new Subject<TMessage>();
        }

        public void Publish(TMessage message)
        {
            _subject.OnNext(message);
        }

        public void Dispose()
        {
            _subject.Dispose();
        }

        public IObservable<TMessage> Messages
        {
            get { return _subject; }
        }
    }
}