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
    }
}