using System;
using System.Reactive.Subjects;
using System.Threading.Tasks;

namespace Obvs
{
    public interface IMessageBus<TMessage> : IMessagePublisher<TMessage>, IMessageSource<TMessage> 
        where TMessage : class 
    {
    }

    public class SubjectMessageBus<TMessage> : IMessageBus<TMessage>
        where TMessage : class
    {
        private readonly Subject<TMessage> _subject;

        public SubjectMessageBus()
        {
            _subject = new Subject<TMessage>();
        }

        public Task PublishAsync(TMessage message)
        {
            _subject.OnNext(message);
            return Task.FromResult(true);
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