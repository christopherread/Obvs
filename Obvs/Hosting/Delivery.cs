using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Obvs.Hosting
{
    // One message's delivery to the bus's subscribers. Each processor it reaches while it's being delivered, a handler
    // or a SubscribeAsync, claims it, and it's settled with its transport once every claim is released: abandoned if a
    // processor failed, released if one stopped before finishing, completed otherwise. A message nothing claims is
    // completed once it's delivered, as subscribers that only observe it don't settle it.
    internal sealed class Delivery
    {
        // Delivery is synchronous, from the transport through the bus's shared stream to each subscriber, so the
        // message being delivered on this thread is the one a processor receiving it can claim
        [ThreadStatic]
        private static Delivery _current;

        private readonly object _message;
        private int _claims = 1; // The delivery's own, until every subscriber has received the message
        private Exception _failure;
        private int _stopped;

        private Delivery(object message, string serviceName, ILogger logger)
        {
            _message = message;
            ServiceName = serviceName;
            Logger = logger;
        }

        public string ServiceName { get; }

        public ILogger Logger { get; }

        public static void Deliver<T>(T message, IObserver<T> observer, string serviceName, ILogger logger)
            where T : class
        {
            var delivery = new Delivery(message, serviceName, logger);
            // A subscriber can deliver another message on this thread before this one's delivery finishes
            var outer = _current;
            _current = delivery;
            try
            {
                observer.OnNext(message);
            }
            finally
            {
                _current = outer;
                _ = delivery.ReleaseAsync(null, false);
            }
        }

        // Claims the message being delivered on this thread for a processor, which releases the claim once it's done.
        // Null if the bus isn't delivering it: it came from elsewhere, or an operator such as ObserveOn passed it on
        // after its delivery, which completed it.
        public static Delivery Claim(object message)
        {
            var delivery = _current;
            if (delivery == null || !ReferenceEquals(delivery._message, message))
            {
                return null;
            }

            Interlocked.Increment(ref delivery._claims);
            return delivery;
        }

        public Task ReleaseAsync(Exception failure, bool stopped)
        {
            if (failure != null)
            {
                Interlocked.CompareExchange(ref _failure, failure, null);
            }
            if (stopped)
            {
                Interlocked.Exchange(ref _stopped, 1);
            }
            if (Interlocked.Decrement(ref _claims) > 0)
            {
                return Task.CompletedTask;
            }

            var firstFailure = Volatile.Read(ref _failure);
            return firstFailure != null ? Settlement.AbandonAsync(_message, firstFailure, ServiceName, Logger)
                : Volatile.Read(ref _stopped) == 1 ? Settlement.ReleaseAsync(_message, ServiceName, Logger)
                : Settlement.CompleteAsync(_message, ServiceName, Logger);
        }
    }
}
