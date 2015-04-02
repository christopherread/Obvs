using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Obvs.RabbitMQ
{
    internal static class RabbitExtensions
    {
        public static QueueingBasicConsumer CreateConsumer(this IModel channel, string exchange, string routingKey)
        {
            QueueDeclareOk queue = channel.QueueDeclare();
            channel.QueueBind(queue, exchange, routingKey);

            QueueingBasicConsumer consumer = new QueueingBasicConsumer(channel);
            channel.BasicConsume(queue, true, consumer);

            return consumer;
        }

        public static IObservable<BasicDeliverEventArgs> GetMessages(this QueueingBasicConsumer consumer)
        {
            const int millisecondsTimeout = 200; // don't want to block indefinitely if task cancelled

            return Observable.Create<BasicDeliverEventArgs>(observer => 
            {
                var tokenSource = new CancellationTokenSource();
                var token = tokenSource.Token;

                var task = Task.Run(() =>
                {
                    try
                    {
                        while (!token.IsCancellationRequested)
                        {
                            BasicDeliverEventArgs msg;
                            if (consumer.Queue.Dequeue(millisecondsTimeout, out msg))
                            {
                                observer.OnNext(msg);
                            }
                        }
                        observer.OnCompleted();
                    }
                    catch (Exception exception)
                    {
                        observer.OnError(exception);
                    }
                }, token);

                return Disposable.Create(() =>
                {
                    tokenSource.Cancel();
                    task.Wait();
                });
            });
        }
    }
}