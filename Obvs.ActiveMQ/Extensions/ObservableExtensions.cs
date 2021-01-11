using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;

namespace Obvs.ActiveMQ.Extensions
{
    internal static class ObservableExtensions
    {
        /// <summary>
        /// This is an effective RefCounting technique for streaming data that you don't expect to end.
        /// It will ref count a working underlying stream (non-completed, non-errored) but will not cache any 
        /// completions/errors unlike the standard .Publish().RefCount().
        /// When subscribed to, this Observable will intelligently resubscribe to the underlying source once 
        /// an existing refcounted stream is completed or errored.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="observable"></param>
        /// <returns></returns>
        internal static IObservable<T> PublishRefCountRetriable<T>(this IObservable<T> observable)
        {
            return observable
                .Multicast(new IndependentSubscriptionsSubject<T>())
                .RefCount();
        }

        private class IndependentSubscriptionsSubject<T> : ISubject<T>
        {
            private ISubject<T> _innerSubject = new Subject<T>();

            public IDisposable Subscribe(IObserver<T> observer)
            {
                return _innerSubject.Subscribe(observer);
            }

            public void OnNext(T value)
            {
                _innerSubject.OnNext(value);
            }

            public void OnCompleted()
            {
                Interlocked.Exchange(ref _innerSubject, new Subject<T>())?.OnCompleted();
            }

            public void OnError(Exception ex)
            {
                Interlocked.Exchange(ref _innerSubject, new Subject<T>())?.OnError(ex);
            }
        }
    }
}