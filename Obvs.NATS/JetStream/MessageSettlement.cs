using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using NATS.Client.Core;
using NATS.Client.JetStream;
using Obvs.Transports;

namespace Obvs.NATS.JetStream
{
    // Settles one received message. Until it's settled, the message is kept in progress, so JetStream doesn't deliver it
    // again while its handler runs.
    internal sealed class MessageSettlement : IMessageSettlement
    {
        // The headers on a message Obvs dead-letters
        public const string DeadLetterReasonHeader = "obvs-dead-letter-reason";
        public const string DeadLetterDescriptionHeader = "obvs-dead-letter-description";
        public const string ExceptionTypeHeader = "obvs-exception-type";
        public const string DeliveryCountHeader = "obvs-delivery-count";
        public const string HandlerFailed = "HandlerFailed";

        private const int MaxDescriptionLength = 4096;

        private readonly INatsJSMsg<byte[]> _message;
        private readonly INatsJSContext _jetStream;
        private readonly string _serviceName;
        private readonly NatsJetStreamOptions _options;
        private readonly TaskCompletionSource<bool> _settled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _settling;

        public MessageSettlement(INatsJSMsg<byte[]> message, INatsJSContext jetStream, string serviceName, NatsJetStreamOptions options)
        {
            _message = message;
            _jetStream = jetStream;
            _serviceName = serviceName;
            _options = options;
        }

        public Task CompleteAsync()
        {
            return SettleAsync(() => _message.AckAsync(new AckOpts { DoubleAck = true }).AsTask());
        }

        // On its last attempt the message is republished to the dead-letter stream with the reason, as JetStream has no
        // dead-letter destination; its delivery limit would only stop delivering it
        public Task AbandonAsync(Exception exception)
        {
            return SettleAsync(() => DeliveryCount >= (ulong)_options.MaxDeliveryAttempts
                ? DeadLetterAsync(exception)
                : _message.NakAsync().AsTask());
        }

        public Task ReleaseAsync()
        {
            return SettleAsync(() => _message.NakAsync().AsTask());
        }

        // The source is stopping: stop waiting. JetStream delivers the message again once its ack wait runs out.
        public void StopWaiting()
        {
            _settled.TrySetResult(false);
        }

        // Waits for the message to be settled, telling JetStream it's in progress every third of its ack wait, for up to
        // MaxInProgressDuration
        public async Task WaitAsync()
        {
            var interval = TimeSpan.FromTicks((_options.AckWait ?? TimeSpan.FromSeconds(30)).Ticks / 3);
            var elapsed = Stopwatch.StartNew();
            while (!_settled.Task.IsCompleted)
            {
                var remaining = _options.MaxInProgressDuration - elapsed.Elapsed;
                if (remaining <= TimeSpan.Zero)
                {
                    await _settled.Task.ConfigureAwait(false);
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

                if (elapsed.Elapsed < _options.MaxInProgressDuration && !_settled.Task.IsCompleted)
                {
                    try
                    {
                        await _message.AckProgressAsync().ConfigureAwait(false);
                    }
                    catch (Exception)
                    {
                        // The connection is closing, or the message was redelivered: JetStream delivers it again either way
                    }
                }
            }
        }

        private ulong DeliveryCount => _message.Metadata?.NumDelivered ?? 1;

        private async Task DeadLetterAsync(Exception exception)
        {
            var headers = new NatsHeaders();
            if (_message.Headers != null)
            {
                foreach (var header in _message.Headers)
                {
                    headers[header.Key] = header.Value;
                }
            }
            headers[DeadLetterReasonHeader] = HandlerFailed;
            headers[DeadLetterDescriptionHeader] = Describe(exception);
            headers[ExceptionTypeHeader] = exception.GetType().FullName;
            headers[DeliveryCountHeader] = DeliveryCount.ToString(CultureInfo.InvariantCulture);

            var ack = await _jetStream.PublishAsync(JetStreamTopology.DeadLetterSubject(_serviceName, _message.Subject), _message.Data, headers: headers)
                .ConfigureAwait(false);
            ack.EnsureSuccess();
            await _message.AckAsync(new AckOpts { DoubleAck = true }).ConfigureAwait(false);
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

        // Header values can't span lines
        private static string Describe(Exception exception)
        {
            var description = $"{exception.GetType().FullName}: {exception.Message}"
                .Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ');
            return description.Length <= MaxDescriptionLength ? description : description.Substring(0, MaxDescriptionLength);
        }
    }
}
