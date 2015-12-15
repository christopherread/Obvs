using System;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;

namespace Obvs
{
    public class ScheduledSubjectMessageBus<TMessage> : IMessageBus<TMessage>
        where TMessage : class
    {
        private readonly IScheduler _scheduler;
        private readonly Subject<TMessage> _subject;

        public ScheduledSubjectMessageBus(IScheduler scheduler)
        {
            _scheduler = scheduler;
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
            get { return _subject.ObserveOn(_scheduler).Publish().RefCount().AsObservable(); }
        }
    }
}