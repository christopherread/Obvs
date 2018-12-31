using System;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Obvs.Extensions;

namespace Obvs
{
    public class SubjectMessageBus<TMessage> : IMessageBus<TMessage>
        where TMessage : class
    {
        private readonly ISubject<TMessage, TMessage> _subject;

        private readonly IObservable<TMessage> _messages;

        public SubjectMessageBus()
            : this(null)
        {
        }

        public SubjectMessageBus(IScheduler scheduler)
        {
            _subject = Subject.Synchronize(new Subject<TMessage>());
            _messages = CreateMessagesObservable(_subject, scheduler);
        }

        private static IObservable<TMessage> CreateMessagesObservable(ISubject<TMessage, TMessage> subject, IScheduler scheduler) {
            return scheduler == null ? 
                subject.AsObservable() : 
                subject.ObserveOn(scheduler)
                        .PublishRefCountRetriable()
                        .AsObservable();
        }

        public Task PublishAsync(TMessage message)
        {
            _subject.OnNext(message);
            return Task.FromResult(true);
        }

        public void Dispose()
        {
            // synchronized subject is anonymous subject underneath,
            // which doesn't implement IDisposable
        }

        public virtual IObservable<TMessage> Messages => _messages;
    
        public IObservable<TMessage> GetMessages(IScheduler scheduler)
        {
            var subject = Subject.Synchronize(new Subject<TMessage>());
            return CreateMessagesObservable(subject, scheduler);
        }
    }
}