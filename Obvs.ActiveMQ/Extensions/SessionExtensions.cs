using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Apache.NMS;

namespace Obvs.ActiveMQ.Extensions
{
    internal static class SessionExtensions
    {
        public static IObservable<IMessage> ToObservable(this ISession session, IDestination destination, string selector = null)
        {
            return Observable.Create<IMessage>(
                observer =>
                {
                    var consumer = string.IsNullOrEmpty(selector) ? session.CreateConsumer(destination) : session.CreateConsumer(destination, selector);
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