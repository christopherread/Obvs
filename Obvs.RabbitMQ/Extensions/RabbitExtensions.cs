using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using RabbitMQ.Client.Events;

namespace Obvs.RabbitMQ.Extensions
{
    internal static class RabbitExtensions
    {
        public static IObservable<BasicDeliverEventArgs> ToObservable(this AsyncEventingBasicConsumer consumer)
        {
            return Observable.Create<BasicDeliverEventArgs>(
                observer =>
                {
                    AsyncEventHandler<BasicDeliverEventArgs> onReceived = (sender, args) =>
                    {
                        observer.OnNext(args);
                        return Task.CompletedTask;
                    };
                    AsyncEventHandler<ConsumerEventArgs> consumerCancelled = (sender, args) =>
                    {
                        observer.OnCompleted();
                        return Task.CompletedTask;
                    };

                    consumer.ReceivedAsync += onReceived;
                    consumer.UnregisteredAsync += consumerCancelled;

                    return Disposable.Create(
                        () =>
                        {
                            consumer.ReceivedAsync -= onReceived;
                            consumer.UnregisteredAsync -= consumerCancelled;
                        });
                });
        }
    }
}
