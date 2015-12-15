using System;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;

namespace Obvs
{
    public class SubjectMessageBus<TMessage> : IMessageBus<TMessage>
        where TMessage : class
    {

        // Could use ScheduledSubjectMessageBus<TMessage> with Scheduler.Immediate but this is simpler
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
            get { return _subject.AsObservable(); }
        }
    }
}