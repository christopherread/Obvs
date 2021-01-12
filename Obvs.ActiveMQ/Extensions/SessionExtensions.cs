using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Apache.NMS;

namespace Obvs.ActiveMQ.Extensions
{
    internal static class SessionExtensions
    {
        public static IObservable<IMessage> ToObservable(this ISession session, IDestination destination, string selector = null, bool noLocal = false)
        {
            return Observable.Create<IMessage>(
                observer =>
                {
                    var consumer = session.CreateConsumer(destination, selector, noLocal);
                    
                    consumer.Listener += observer.OnNext;

                    return Disposable.Create(
                        () =>
                        {
                            consumer.Listener -= observer.OnNext;
                            consumer.Close();
                            consumer.Dispose();
                            consumer = null;
                        });
                });
        }
    }
}