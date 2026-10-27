using System;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Obvs.Extensions;

// v6's local in-memory bus, kept for v6's fluent configuration; v7 uses Obvs.Testing's in-memory transport: its public members are exempt from the rule that every one is documented
#pragma warning disable CS1570, CS1572, CS1591

namespace Obvs
{
    public class SubjectMessageBus<TMessage> : IMessageBus<TMessage>
        where TMessage : class
    {
        private readonly ISubject<TMessage, TMessage> _subject;

        public SubjectMessageBus()
            : this(null)
        {
        }

        public SubjectMessageBus(IScheduler scheduler)
        {
            _subject = Subject.Synchronize(new Subject<TMessage>());

            Messages = scheduler == null ? 
                _subject.AsObservable() : 
                _subject.ObserveOn(scheduler)
                        .PublishRefCountRetriable()
                        .AsObservable();
        }

        public Task PublishAsync(TMessage message, CancellationToken cancellationToken = default)
        {
            _subject.OnNext(message);
            return Task.FromResult(true);
        }

        public void Dispose()
        {
            // synchronized subject is annonymous subject underneath,
            // which doesn't implment IDisposable
        }

        public IObservable<TMessage> Messages { get; private set; }
    }
}