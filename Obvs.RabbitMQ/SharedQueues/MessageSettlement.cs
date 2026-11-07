using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Obvs.Transports;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Obvs.RabbitMQ.SharedQueues
{
    // Settles one message delivered from a shared queue. Until it's settled, the broker counts it against the channel's
    // prefetch, and gives it back to the queue if the channel closes.
    internal sealed class MessageSettlement : IMessageSettlement
    {
        // The headers on a message Obvs dead-letters
        public const string DeadLetterReasonHeader = "obvs-dead-letter-reason";
        public const string DeadLetterDescriptionHeader = "obvs-dead-letter-description";
        public const string ExceptionTypeHeader = "obvs-exception-type";
        public const string DeliveryCountHeader = "obvs-delivery-count";
        public const string HandlerFailed = "HandlerFailed";
        public const string Undeserializable = "Undeserializable";

        // Set by quorum queues on a message delivered before: how many times it was rejected, or on RabbitMQ 3, returned
        public const string BrokerDeliveryCountHeader = "x-delivery-count";
        private const int MaxDescriptionLength = 4096;

        private readonly IChannel _channel;
        private readonly BasicDeliverEventArgs _delivery;
        private readonly byte[] _body;
        private readonly SharedQueueTopology _topology;
        private int _settling;

        // The body is a copy, as the delivery's buffer is only valid until its handler returns
        public MessageSettlement(IChannel channel, BasicDeliverEventArgs delivery, byte[] body, SharedQueueTopology topology)
        {
            _channel = channel;
            _delivery = delivery;
            _body = body;
            _topology = topology;
        }

        public long DeliveryCount
        {
            get
            {
                var headers = _delivery.BasicProperties.Headers;
                return headers != null && headers.TryGetValue(BrokerDeliveryCountHeader, out var count) && count != null
                    ? Convert.ToInt64(count, CultureInfo.InvariantCulture) + 1
                    : 1;
            }
        }

        public Task CompleteAsync()
        {
            return SettleAsync(() => _channel.BasicAckAsync(_delivery.DeliveryTag, false).AsTask());
        }

        public Task AbandonAsync(Exception exception)
        {
            return AbandonAsync(exception, HandlerFailed);
        }

        // On its last attempt the message is dead-lettered with the reason, rather than left to the queue's delivery limit,
        // which would record only that it reached it
        public Task AbandonAsync(Exception exception, string reason)
        {
            return SettleAsync(() => DeliveryCount >= _topology.MaxDeliveryAttempts
                ? DeadLetterAsync(exception, reason)
                : _channel.BasicRejectAsync(_delivery.DeliveryTag, true).AsTask());
        }

        // Unlike a reject, a nack doesn't count as a failed delivery on RabbitMQ 4, so the message keeps its attempts
        public Task ReleaseAsync()
        {
            return SettleAsync(() => _channel.BasicNackAsync(_delivery.DeliveryTag, false, true).AsTask());
        }

        private async Task DeadLetterAsync(Exception exception, string reason)
        {
            var headers = new Dictionary<string, object>();
            if (_delivery.BasicProperties.Headers != null)
            {
                foreach (var header in _delivery.BasicProperties.Headers)
                {
                    headers[header.Key] = header.Value;
                }
            }
            headers[DeadLetterReasonHeader] = reason;
            headers[DeadLetterDescriptionHeader] = Describe(exception);
            headers[ExceptionTypeHeader] = exception.GetType().FullName;
            headers[DeliveryCountHeader] = DeliveryCount.ToString(CultureInfo.InvariantCulture);
            var properties = new BasicProperties(_delivery.BasicProperties) { Headers = headers };

            await _channel.BasicPublishAsync(_topology.DeadLetters, _delivery.RoutingKey, false, properties, _body).ConfigureAwait(false);
            await _channel.BasicAckAsync(_delivery.DeliveryTag, false).ConfigureAwait(false);
        }

        // Only the first settlement counts, as one message can reach several subscribers
        private Task SettleAsync(Func<Task> settle)
        {
            return Interlocked.Exchange(ref _settling, 1) == 1 ? Task.CompletedTask : settle();
        }

        // Header values can't span lines
        private static string Describe(Exception exception)
        {
            var description = $"{exception.GetType().FullName}: {exception.Message}"
                .Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ');
            return description.Length <= MaxDescriptionLength ? description : description.Substring(0, MaxDescriptionLength);
        }
    }
}
