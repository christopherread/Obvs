using System;
using System.Reactive.Concurrency;
using System.Reactive.Linq;

namespace Obvs.Extensions
{
    internal static class ObservableExtensions
    {
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