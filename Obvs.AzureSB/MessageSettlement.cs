using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;

namespace Obvs.AzureSB
{
    // Settles one received message through the processor callback that received it. The callback waits for
    // Settled, so the processor keeps renewing the message's lock until the handler has run.
    internal sealed class MessageSettlement : IMessageSettlement
    {
        // The reason recorded on messages Obvs dead-letters, and the property naming the handler's exception type
        public const string HandlerFailed = "HandlerFailed";
        public const string ExceptionTypeProperty = "obvs-exception-type";

        // Service Bus allows dead-letter descriptions of up to 4096 characters
        private const int MaxDescriptionLength = 4096;

        private readonly ProcessMessageEventArgs _args;
        private readonly int _maxDeliveryAttempts;
        private readonly TaskCompletionSource<bool> _settled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _settling;

        public MessageSettlement(ProcessMessageEventArgs args, int maxDeliveryAttempts)
        {
            _args = args;
            _maxDeliveryAttempts = maxDeliveryAttempts;
        }

        public Task Settled => _settled.Task;

        public Task CompleteAsync()
        {
            return SettleAsync(() => _args.CompleteMessageAsync(_args.Message));
        }

        // On its last attempt the message is dead-lettered with the reason, rather than left to Service Bus's own
        // delivery limit, which would record only that the limit was reached
        public Task AbandonAsync(Exception exception)
        {
            var properties = new Dictionary<string, object> { [ExceptionTypeProperty] = exception.GetType().FullName };
            return SettleAsync(() => _args.Message.DeliveryCount >= _maxDeliveryAttempts
                ? _args.DeadLetterMessageAsync(_args.Message, properties, HandlerFailed, Describe(exception))
                : _args.AbandonMessageAsync(_args.Message, properties));
        }

        public Task ReleaseAsync()
        {
            return SettleAsync(() => _args.AbandonMessageAsync(_args.Message));
        }

        // The source is stopping: stop waiting. Service Bus delivers the message again once its lock expires.
        public void StopWaiting()
        {
            _settled.TrySetResult(false);
        }

        // Only the first settlement counts, as one message can reach several subscribers
        private async Task SettleAsync(Func<Task> settle)
        {
            if (Interlocked.Exchange(ref _settling, 1) == 1)
            {
                return;
            }

            try
            {
                await settle().ConfigureAwait(false);
            }
            finally
            {
                _settled.TrySetResult(true);
            }
        }

        private static string Describe(Exception exception)
        {
            var description = $"{exception.GetType().FullName}: {exception.Message}";
            return description.Length <= MaxDescriptionLength ? description : description.Substring(0, MaxDescriptionLength);
        }
    }
}
