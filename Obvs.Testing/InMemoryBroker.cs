using System;
using System.Collections.Concurrent;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Obvs.Types;

namespace Obvs.Testing
{
    /// <summary>
    /// A message broker that lives in memory. Services that call <c>UseInMemory()</c> share one broker
    /// per container, so each host is isolated; pass the same broker to several hosts to connect them.
    /// </summary>
    public sealed class InMemoryBroker
    {
        private readonly IScheduler _scheduler;
        private readonly ConcurrentDictionary<string, ISubject<IMessage>> _destinations = new ConcurrentDictionary<string, ISubject<IMessage>>();
        private readonly ISubject<InMemoryMessage> _messages = Subject.Synchronize(new Subject<InMemoryMessage>());

        /// <summary>Creates a broker that delivers messages on the task pool, as a real broker's client threads would.</summary>
        public InMemoryBroker()
            : this(null)
        {
        }

        /// <summary>
        /// Creates a broker that delivers messages on <paramref name="scheduler"/>.
        /// Pass a <c>TestScheduler</c> to control exactly when messages are delivered in a test.
        /// </summary>
        public InMemoryBroker(IScheduler scheduler)
        {
            _scheduler = scheduler ?? TaskPoolScheduler.Default;
        }

        /// <summary>Every message passing through the broker, in the order it was sent, before it's delivered.</summary>
        public IObservable<InMemoryMessage> Messages => _messages.AsObservable();

        internal void Send(string serviceName, string destination, IMessage message)
        {
            _messages.OnNext(new InMemoryMessage(serviceName, message));
            GetDestination(destination).OnNext(message);
        }

        // Each subscriber gets its own ordered queue on the scheduler, like a consumer on a real broker
        internal IObservable<IMessage> Receive(string destination)
        {
            return GetDestination(destination).ObserveOn(_scheduler);
        }

        private ISubject<IMessage> GetDestination(string destination)
        {
            return _destinations.GetOrAdd(destination, _ => Subject.Synchronize(new Subject<IMessage>()));
        }
    }

    /// <summary>A message passing through an <see cref="InMemoryBroker"/>.</summary>
    public sealed class InMemoryMessage
    {
        public InMemoryMessage(string serviceName, IMessage message)
        {
            ServiceName = serviceName;
            Message = message;
        }

        /// <summary>The name of the service the message belongs to.</summary>
        public string ServiceName { get; }

        public IMessage Message { get; }
    }
}
