using System;
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
            return observable.Catch<T, Exception>(exception => Handle(exception, continueWith().CatchAndHandle(observer, continueWith, message), observer, message));
        }

        private static IObservable<T> Handle<T>(Exception exception, IObservable<T> observable, IObserver<Exception> observer, string message)
        {
            observer.OnNext(new Exception(message, exception));
            return observable;
        }
    }
}