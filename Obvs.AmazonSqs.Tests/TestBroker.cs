using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using Amazon.SQS;
using Amazon.SQS.Model;

namespace Obvs.AmazonSqs.Tests
{
    internal static class TestBroker
    {
        // LocalStack from LocalStack/docker-compose.yml. Set AWS_SERVICE_URL to use another endpoint.
        public static string ServiceUrl => Environment.GetEnvironmentVariable("AWS_SERVICE_URL") ?? "http://localhost:4566";

        public const string Region = "us-east-1";

        // LocalStack accepts any credentials
        public static AWSCredentials Credentials { get; } = new BasicAWSCredentials("test", "test");

        public static void Configure(AmazonSqsOptions options)
        {
            options.Region = Region;
            options.ServiceUrl = ServiceUrl;
            options.Credentials = Credentials;
        }

        public static IAmazonSQS CreateSqsClient()
        {
            return new AmazonSQSClient(Credentials, new AmazonSQSConfig { ServiceURL = ServiceUrl, AuthenticationRegion = Region });
        }

        public static IAmazonSimpleNotificationService CreateSnsClient()
        {
            return new AmazonSimpleNotificationServiceClient(Credentials, new AmazonSimpleNotificationServiceConfig { ServiceURL = ServiceUrl, AuthenticationRegion = Region });
        }

        // The queue's URL, or null if it doesn't exist
        public static async Task<string> QueueUrlAsync(string queueName, CancellationToken ct)
        {
            using var sqs = CreateSqsClient();
            try
            {
                return (await sqs.GetQueueUrlAsync(queueName, ct)).QueueUrl;
            }
            catch (QueueDoesNotExistException)
            {
                return null;
            }
        }

        public static async Task<Dictionary<string, string>> QueueAttributesAsync(string queueName, CancellationToken ct)
        {
            using var sqs = CreateSqsClient();
            var url = (await sqs.GetQueueUrlAsync(queueName, ct)).QueueUrl;
            return (await sqs.GetQueueAttributesAsync(new GetQueueAttributesRequest { QueueUrl = url, AttributeNames = new List<string> { "All" } }, ct)).Attributes;
        }

        public static async Task<Dictionary<string, string>> QueueTagsAsync(string queueName, CancellationToken ct)
        {
            using var sqs = CreateSqsClient();
            var url = (await sqs.GetQueueUrlAsync(queueName, ct)).QueueUrl;
            return (await sqs.ListQueueTagsAsync(new ListQueueTagsRequest { QueueUrl = url }, ct)).Tags ?? new Dictionary<string, string>();
        }

        // Messages on the queue, whether a receiver has them in hand or not
        public static async Task<int> MessagesOnQueueAsync(string queueName, CancellationToken ct)
        {
            var attributes = await QueueAttributesAsync(queueName, ct);
            return int.Parse(attributes["ApproximateNumberOfMessages"]) + int.Parse(attributes["ApproximateNumberOfMessagesNotVisible"]);
        }
    }
}
