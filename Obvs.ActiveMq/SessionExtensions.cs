using System;
using System.IO;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Apache.NMS;

namespace Obvs.ActiveMq
{
    public static class SessionExtensions
    {
        public static IObservable<IMessage> ToObservable(this ISession session, IDestination destination)
        {
            return session.ToObservable(destination, null);
        }

        public static IObservable<IMessage> ToObservable(this ISession session, IDestination destination, string selector)
        {
            return Observable.Create<IMessage>(
                observer =>
                {
                    IMessageConsumer consumer;
                    consumer = string.IsNullOrEmpty(selector) ? session.CreateConsumer(destination) : session.CreateConsumer(destination, selector);
                    consumer.Listener += observer.OnNext;

                    return Disposable.Create(
                        () =>
                        {
                            consumer.Listener -= observer.OnNext;
                            consumer.Close();
                            consumer.Dispose();
                            consumer = null;
                        }
                        );
                });
        }

        public static IMessage CreateMessageFromData(this ISession session, object data)
        {
            string text = data as string;
            if (text != null)
            {
                return session.CreateTextMessage(text);
            }

            byte[] body = data as byte[];
            if (body != null)
            {
                return session.CreateBytesMessage(body);
            }

            throw new ArgumentException("Data must be a string or a byte[] to be converted to an ActiveMq message, but is " + (data == null ? "null" : data.GetType().FullName));
        }
    }
}