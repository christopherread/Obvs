using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using NATS.Client;

namespace Obvs.NATS
{
    internal static class AsyncSubscriptionExtensions
    {
        public static IObservable<MsgHandlerEventArgs> ToObservable(this IAsyncSubscription sub)
        {
            return Observable.Create<MsgHandlerEventArgs>(
                observer =>
                {
                    EventHandler<MsgHandlerEventArgs> handler = (sender, args) => observer.OnNext(args);
                    sub.MessageHandler += handler;
                    sub.Start();
                    return Disposable.Create(() =>
                    {
                        sub.MessageHandler -= handler;
                        sub.Unsubscribe();
                    });
                });
        }
    }
}