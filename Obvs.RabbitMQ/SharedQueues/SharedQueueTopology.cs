using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Obvs.RabbitMQ.SharedQueues
{
    // The queues a service's buses share for its commands and requests, and where those are dead-lettered. Messages are
    // published to the service's exchange, as in v6, so v6 apps still reach the queues, and v7 apps v6's.
    internal sealed class SharedQueueTopology
    {
        private const ushort PreconditionFailed = 406;

        public SharedQueueTopology(string serviceName, int maxDeliveryAttempts)
        {
            ServiceName = serviceName;
            MaxDeliveryAttempts = maxDeliveryAttempts;
            DeadLetters = $"{serviceName}.DeadLetters";
        }

        public string ServiceName { get; }

        public int MaxDeliveryAttempts { get; }

        // The name of both the dead-letter exchange and the queue that keeps what's sent to it
        public string DeadLetters { get; }

        // Declares the exchanges and queues, and binds the queue to the messages published for it. A declaration that
        // fails closes the channel.
        public async Task DeclareAsync(IChannel channel, string queue)
        {
            // As v6 declares it, as declaring an exchange that exists with other settings fails
            await channel.ExchangeDeclareAsync(ServiceName, ExchangeType.Topic).ConfigureAwait(false);
            await channel.ExchangeDeclareAsync(DeadLetters, ExchangeType.Topic, durable: true, autoDelete: false).ConfigureAwait(false);
            await DeclareQueueAsync(channel, DeadLetters, new Dictionary<string, object> { ["x-queue-type"] = "quorum" }).ConfigureAwait(false);
            await channel.QueueBindAsync(DeadLetters, DeadLetters, "#").ConfigureAwait(false);

            await DeclareQueueAsync(channel, queue, new Dictionary<string, object>
            {
                ["x-queue-type"] = "quorum",
                ["x-delivery-limit"] = MaxDeliveryAttempts,
                ["x-dead-letter-exchange"] = DeadLetters,
            }).ConfigureAwait(false);
            await channel.QueueBindAsync(queue, ServiceName, $"{queue}.*").ConfigureAwait(false);
        }

        private async Task DeclareQueueAsync(IChannel channel, string queue, IDictionary<string, object> arguments)
        {
            try
            {
                await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false, arguments: arguments).ConfigureAwait(false);
            }
            catch (OperationInterruptedException exception) when (exception.ShutdownReason?.ReplyCode == PreconditionFailed)
            {
                throw new InvalidOperationException(
                    $"Service '{ServiceName}' needs RabbitMQ queue '{queue}' to be a durable quorum queue, with a delivery limit of {MaxDeliveryAttempts} " +
                    $"(MaxDeliveryAttempts) and dead-letter exchange '{DeadLetters}', but it exists with other settings: {exception.ShutdownReason.ReplyText}. " +
                    "Delete the queue, and the bus declares it again. Its messages go with it, so move any you need first, with a shovel for example.",
                    exception);
            }
        }
    }
}
