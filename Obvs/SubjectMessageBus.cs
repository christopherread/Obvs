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
        private readonly Subject<TMessage> _subject;

        public SubjectMessageBus()
            : this(null)
        {
        }

        public SubjectMessageBus(IScheduler scheduler)
        {
            _subject = new Subject<TMessage>();

            Messages = scheduler == null ? _subject.AsObservable() : _subject.ObserveOn(scheduler).PublishRefCountRetriable().AsObservable();
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

        public IObservable<TMessage> Messages { get; private set; }
    }
}