using System;
using System.Reactive.Linq;

namespace Obvs.Extensions
{
    internal static class ObservableExtensions
    {
        public static IObservable<T> CatchAndHandle<T>(this IObservable<T> observable, IObserver<Exception> observer, Func<IObservable<T>> continueWith, string message)
        {
            return observable.Catch<T, Exception>(exception => Handle(exception, continueWith().CatchAndHandle(observer, continueWith, message), observer, message));
        }

        private static IObservable<T> Handle<T>(Exception exception, IObservable<T> observable, IObserver<Exception> observer, string message)
        {
            observer.OnNext(new Exception(message, exception));
            return observable;
        }
    }
}