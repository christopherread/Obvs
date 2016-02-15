using System;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Obvs.Extensions
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
            private volatile ISubject<T> _innerSubject = new Subject<T>();

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
                var completingInnerSubject = _innerSubject;
                _innerSubject = new Subject<T>();
                completingInnerSubject.OnCompleted();
            }

            public void OnError(Exception ex)
            {
                var erroringInnerSubject = _innerSubject;
                _innerSubject = new Subject<T>();
                erroringInnerSubject.OnError(ex);
            }
        }

        internal static IObservable<T> CatchAndHandle<T>(this IObservable<T> observable, IObserver<Exception> observer, Func<IObservable<T>> continueWith, string message)
        {
            //return observable.RetryWithBackoffStrategy(continueWith, observer, message, 2);
            return observable.Catch<T, Exception>(exception => Handle(exception, continueWith().CatchAndHandle(observer, continueWith, message), observer, message));
        }

        private static IObservable<T> Handle<T>(Exception exception, IObservable<T> observable, IObserver<Exception> observer, string message)
        {
            observer.OnNext(new Exception(message, exception));
            return observable;
        }

        internal static readonly Func<int, TimeSpan> ExponentialBackoff = n => TimeSpan.FromSeconds(Math.Pow(n, 2));

        internal static IObservable<T> RetryWithBackoffStrategy<T>(
            this IObservable<T> source,
            Func<IObservable<T>> continueWith,
            IObserver<Exception> observer,
            string message,
            int retryCount = 1,
            Func<int, TimeSpan> strategy = null,
            Func<Exception, bool> retryOnError = null,
            IScheduler scheduler = null)
        {
            strategy = strategy ?? ExponentialBackoff;
            retryOnError = retryOnError ?? (exception => true);

            int attempt = 0;
            
            return Observable.Defer(() =>
            {
                return ((++attempt == 1) ? source : continueWith().DelaySubscription(strategy(attempt - 1), scheduler))
                    .Select(item => new Tuple<bool, T, Exception>(true, item, null))
                    .Catch<Tuple<bool, T, Exception>, Exception>(e =>
                    {
                        observer.OnNext(new Exception(message, e));
                        return retryOnError(e)
                            ? Observable.Throw<Tuple<bool, T, Exception>>(e)
                            : Observable.Return(new Tuple<bool, T, Exception>(false, default(T), e));
                    });
            })
            .Retry(retryCount)
            .SelectMany(t => t.Item1 ? Observable.Return(t.Item2) : 
                                       Observable.Throw<T>(t.Item3));
        }

        internal static IObservable<T> DelaySubscription<T>(this IObservable<T> source,
            TimeSpan delay, IScheduler scheduler = null)
        {
            if (scheduler == null)
            {
                return Observable.Timer(delay).SelectMany(_ => source);
            }
            return Observable.Timer(delay, scheduler).SelectMany(_ => source);
        }
    }
}