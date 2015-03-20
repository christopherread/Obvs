using System;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Obvs.RabbitMQ
{
    internal static class RabbitExtensions
    {
        public static Task<BasicDeliverEventArgs> DequeueAsync(this QueueingBasicConsumer consumer, CancellationToken token)
        {
            return Task.Run(() => consumer.Queue.Dequeue(), token);
        }

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
            return Observable.Defer(() => Observable.StartAsync(consumer.DequeueAsync)).Repeat();
        }
    }
}