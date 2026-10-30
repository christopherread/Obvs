using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Obvs.Transports;
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
        private readonly ConcurrentDictionary<string, ISubject<InMemoryMessage>> _destinations = new ConcurrentDictionary<string, ISubject<InMemoryMessage>>();
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

        internal void Send(string serviceName, string destination, IMessage message, IReadOnlyDictionary<string, string> headers)
        {
            var sent = new InMemoryMessage(serviceName, message, headers.ToDictionary(header => header.Key, header => header.Value));
            _messages.OnNext(sent);
            GetDestination(destination).OnNext(sent);
        }

        // Each subscriber gets its own ordered queue on the scheduler, like a consumer on a real broker. Unlike a
        // broker's, the message delivered is the instance that was sent, so its headers are attached as it's delivered:
        // one instance sent twice at once is delivered with whichever headers were attached last.
        internal IObservable<IMessage> Receive(string destination)
        {
            return GetDestination(destination).ObserveOn(_scheduler).Select(received =>
            {
                ObvsReceivedMessage.Attach(received.Message, received.Headers);
                return received.Message;
            });
        }

        private ISubject<InMemoryMessage> GetDestination(string destination)
        {
            return _destinations.GetOrAdd(destination, _ => Subject.Synchronize(new Subject<InMemoryMessage>()));
        }
    }

    /// <summary>A message passing through an <see cref="InMemoryBroker"/>.</summary>
    public sealed class InMemoryMessage
    {
        /// <summary>A message, the service it was sent on, and the headers it was sent with.</summary>
        /// <param name="serviceName">The service's name</param>
        /// <param name="message">The message</param>
        /// <param name="headers">The headers it was sent with</param>
        public InMemoryMessage(string serviceName, IMessage message, IReadOnlyDictionary<string, string> headers)
        {
            ServiceName = serviceName;
            Message = message;
            Headers = headers;
        }

        /// <summary>The name of the service the message belongs to.</summary>
        public string ServiceName { get; }

        /// <summary>The message.</summary>
        public IMessage Message { get; }

        /// <summary>The headers it was sent with: the sender's, and its trace context.</summary>
        public IReadOnlyDictionary<string, string> Headers { get; }
    }
}
