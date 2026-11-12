using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SQS;
using Amazon.SQS.Model;
using Obvs.Transports;

namespace Obvs.AmazonSqs
{
    // Settles one received message on the queue it came from. Until it's settled, it holds one of the receive loop's
    // MaxConcurrentMessages places, which onSettled gives back, and its visibility is extended so SQS doesn't deliver
    // it again while its handler runs.
    internal sealed class MessageSettlement : IMessageSettlement
    {
        // Attributes recording why Obvs dead-lettered a message
        public const string HandlerFailed = "HandlerFailed";
        public const string ReasonAttribute = "obvs-dead-letter-reason";
        public const string DescriptionAttribute = "obvs-dead-letter-description";
        public const string ExceptionTypeAttribute = "obvs-exception-type";
        public const string DeliveryAttemptsAttribute = "obvs-delivery-attempts";

        public const string ReceiveCount = "ApproximateReceiveCount";

        private const int MaxDescriptionLength = 4096;

        private readonly IAmazonSQS _sqs;
        private readonly string _queueUrl;
        private readonly string _deadLetterQueueUrl;
        private readonly Message _message;
        private readonly int _maxDeliveryAttempts;
        private readonly TimeSpan _visibilityTimeout;
        private readonly TimeSpan _maxHandlerDuration;
        private readonly Action _onSettled;
        private readonly TaskCompletionSource<bool> _settled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _settling;

        public MessageSettlement(IAmazonSQS sqs, string queueUrl, string deadLetterQueueUrl, Message message, int maxDeliveryAttempts,
            TimeSpan visibilityTimeout, TimeSpan maxHandlerDuration, Action onSettled)
        {
            _sqs = sqs;
            _queueUrl = queueUrl;
            _deadLetterQueueUrl = deadLetterQueueUrl;
            _message = message;
            _maxDeliveryAttempts = maxDeliveryAttempts;
            _visibilityTimeout = visibilityTimeout;
            _maxHandlerDuration = maxHandlerDuration;
            _onSettled = onSettled;
        }

        public Task Settled => _settled.Task;

        public Task CompleteAsync()
        {
            return SettleAsync(DeleteAsync);
        }

        // On its last attempt the message is moved to the dead-letter queue with the reason, rather than left to the
        // queue's redrive policy, which would record only that the receive count was reached
        public Task AbandonAsync(Exception exception)
        {
            return SettleAsync(() => DeliveryAttempts >= _maxDeliveryAttempts ? DeadLetterAsync(exception) : MakeVisibleAsync());
        }

        public Task ReleaseAsync()
        {
            return SettleAsync(MakeVisibleAsync);
        }

        // The source is stopping: give back the message's place. SQS delivers it again once its visibility timeout passes.
        public void StopWaiting()
        {
            if (Interlocked.Exchange(ref _settling, 1) == 0)
            {
                Finish();
            }
        }

        // Until the message is settled, extends its visibility every third of the visibility timeout, for up to
        // MaxHandlerDuration from when it was received, as SQS doesn't know a handler is still running it
        public async Task KeepHiddenAsync()
        {
            var interval = TimeSpan.FromTicks(_visibilityTimeout.Ticks / 3);
            var elapsed = Stopwatch.StartNew();
            while (!_settled.Task.IsCompleted)
            {
                var remaining = _maxHandlerDuration - elapsed.Elapsed;
                if (remaining <= TimeSpan.Zero)
                {
                    return;
                }

                using (var cancellation = new CancellationTokenSource())
                {
                    var delay = Task.Delay(remaining < interval ? remaining : interval, cancellation.Token);
                    if (await Task.WhenAny(_settled.Task, delay).ConfigureAwait(false) == _settled.Task)
                    {
                        cancellation.Cancel();
                        return;
                    }
                }

                if (elapsed.Elapsed < _maxHandlerDuration && Volatile.Read(ref _settling) == 0)
                {
                    try
                    {
                        await _sqs.ChangeMessageVisibilityAsync(new ChangeMessageVisibilityRequest
                        {
                            QueueUrl = _queueUrl,
                            ReceiptHandle = _message.ReceiptHandle,
                            VisibilityTimeout = (int)Math.Ceiling(_visibilityTimeout.TotalSeconds),
                        }).ConfigureAwait(false);
                    }
                    catch (Exception)
                    {
                        // The message was settled meanwhile, or SQS can't be reached: SQS delivers it again either way
                    }
                }
            }
        }

        private int DeliveryAttempts =>
            _message.Attributes != null && _message.Attributes.TryGetValue(ReceiveCount, out var count) && int.TryParse(count, out var attempts) ? attempts : 1;

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
                Finish();
            }
        }

        private void Finish()
        {
            _settled.TrySetResult(true);
            _onSettled();
        }

        private Task DeleteAsync()
        {
            return _sqs.DeleteMessageAsync(new DeleteMessageRequest { QueueUrl = _queueUrl, ReceiptHandle = _message.ReceiptHandle });
        }

        private Task MakeVisibleAsync()
        {
            return _sqs.ChangeMessageVisibilityAsync(new ChangeMessageVisibilityRequest { QueueUrl = _queueUrl, ReceiptHandle = _message.ReceiptHandle, VisibilityTimeout = 0 });
        }

        private async Task DeadLetterAsync(Exception exception)
        {
            var attributes = new Dictionary<string, MessageAttributeValue>();
            if (_message.MessageAttributes != null)
            {
                foreach (var attribute in _message.MessageAttributes)
                {
                    attributes[attribute.Key] = attribute.Value;
                }
            }
            attributes[ReasonAttribute] = StringAttribute(HandlerFailed);
            attributes[DescriptionAttribute] = StringAttribute(Describe(exception));
            attributes[ExceptionTypeAttribute] = StringAttribute(exception.GetType().FullName);
            attributes[DeliveryAttemptsAttribute] = StringAttribute(DeliveryAttempts.ToString(CultureInfo.InvariantCulture));

            await _sqs.SendMessageAsync(new SendMessageRequest { QueueUrl = _deadLetterQueueUrl, MessageBody = _message.Body, MessageAttributes = attributes }).ConfigureAwait(false);
            await DeleteAsync().ConfigureAwait(false);
        }

        private static MessageAttributeValue StringAttribute(string value)
        {
            return new MessageAttributeValue { DataType = MessageCodec.StringDataType, StringValue = value };
        }

        private static string Describe(Exception exception)
        {
            var description = $"{exception.GetType().FullName}: {exception.Message}";
            return description.Length <= MaxDescriptionLength ? description : description.Substring(0, MaxDescriptionLength);
        }
    }
}
