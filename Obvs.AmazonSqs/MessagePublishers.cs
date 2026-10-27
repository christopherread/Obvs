using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Obvs.Serialization;

namespace Obvs.AmazonSqs
{
    // Sends commands and requests to a queue the service's buses share
    internal sealed class QueueMessagePublisher<TMessage> : IMessagePublisher<TMessage>
        where TMessage : class
    {
        private readonly IAmazonSQS _sqs;
        private readonly string _queueUrl;
        private readonly IMessageSerializer _serializer;
        private readonly string _replyTo;

        /// <param name="sqs">Shared with the endpoint's sources, and disposed with the endpoint</param>
        /// <param name="replyTo">The URL of the queue responses should go to, for requests</param>
        public QueueMessagePublisher(IAmazonSQS sqs, string queueUrl, IMessageSerializer serializer, string replyTo = null)
        {
            _sqs = sqs;
            _queueUrl = queueUrl;
            _serializer = serializer;
            _replyTo = replyTo;
        }

        public Task PublishAsync(TMessage message)
        {
            return SendAsync(_sqs, _queueUrl, message, _serializer, _replyTo);
        }

        public static async Task SendAsync(IAmazonSQS sqs, string queueUrl, object message, IMessageSerializer serializer, string replyTo = null)
        {
            var encoded = MessageCodec.Encode(message, serializer, replyTo);
            await sqs.SendMessageAsync(new SendMessageRequest
            {
                QueueUrl = queueUrl,
                MessageBody = encoded.Body,
                MessageAttributes = encoded.Attributes.ToDictionary(
                    attribute => attribute.Key,
                    attribute => new Amazon.SQS.Model.MessageAttributeValue { DataType = MessageCodec.StringDataType, StringValue = attribute.Value }),
            }).ConfigureAwait(false);
        }

        public void Dispose()
        {
        }
    }

    // Publishes events to the service's topic, which delivers them to every subscribing bus's queue
    internal sealed class TopicMessagePublisher<TMessage> : IMessagePublisher<TMessage>
        where TMessage : class
    {
        private readonly IAmazonSimpleNotificationService _sns;
        private readonly string _topicArn;
        private readonly IMessageSerializer _serializer;

        /// <param name="sns">Disposed with the endpoint</param>
        public TopicMessagePublisher(IAmazonSimpleNotificationService sns, string topicArn, IMessageSerializer serializer)
        {
            _sns = sns;
            _topicArn = topicArn;
            _serializer = serializer;
        }

        public async Task PublishAsync(TMessage message)
        {
            var encoded = MessageCodec.Encode(message, _serializer);
            await _sns.PublishAsync(new PublishRequest
            {
                TopicArn = _topicArn,
                Message = encoded.Body,
                MessageAttributes = encoded.Attributes.ToDictionary(
                    attribute => attribute.Key,
                    attribute => new Amazon.SimpleNotificationService.Model.MessageAttributeValue { DataType = MessageCodec.StringDataType, StringValue = attribute.Value }),
            }).ConfigureAwait(false);
        }

        public void Dispose()
        {
        }
    }
}
