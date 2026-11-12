using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using Amazon.SQS.Model;

namespace Obvs.AmazonSqs
{
    // The queues crashed buses of a service left behind. SQS never deletes a queue itself, and the service's topic keeps
    // sending events to a crashed bus's event queue, so each running bus records on its own queues when it was last seen,
    // and deletes other buses' queues once they've gone unseen for longer than those buses said they might.
    internal static class AbandonedQueues
    {
        public const string LastSeenTag = "obvs-last-seen";
        public const string AbandonedAfterTag = "obvs-abandoned-after";

        // In UTC, to the second, which SQS tag values allow
        private const string LastSeenFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";

        // The most ListQueues returns at once
        private const int MaxQueuesPerPage = 1000;

        private static readonly long MaxAbandonedAfterSeconds = (long)TimeSpan.MaxValue.TotalSeconds;

        // When the bus was last seen, and how long it may go unseen before other buses take it for crashed
        public static Dictionary<string, string> LastSeenTags(AmazonSqsOptions options, DateTimeOffset now)
        {
            return new Dictionary<string, string>
            {
                [LastSeenTag] = now.UtcDateTime.ToString(LastSeenFormat, CultureInfo.InvariantCulture),
                [AbandonedAfterTag] = ((long)Math.Ceiling(options.AbandonedAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture),
            };
        }

        // Only another bus's own queue of this service counts, and only once it's gone unseen for longer than that bus's
        // window. Queues without both tags, or with tags that can't be read, are left alone.
        public static bool IsAbandoned(string queueName, IDictionary<string, string> tags, string serviceName, string ownInstanceId, DateTimeOffset now)
        {
            if (!tags.TryGetValue(AmazonSqsEntities.ServiceTag, out var service) || service != serviceName)
            {
                return false;
            }
            if (!tags.TryGetValue(AmazonSqsEntities.InstanceIdTag, out var instanceId) || instanceId == ownInstanceId)
            {
                return false;
            }
            var busQueues = new AmazonSqsEntities(serviceName, instanceId, null);
            if (queueName != busQueues.EventQueue && queueName != busQueues.Replies)
            {
                return false;
            }
            if (!tags.TryGetValue(LastSeenTag, out var lastSeenText) ||
                !DateTimeOffset.TryParseExact(lastSeenText, LastSeenFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var lastSeen))
            {
                return false;
            }
            if (!tags.TryGetValue(AbandonedAfterTag, out var abandonedAfterText) ||
                !long.TryParse(abandonedAfterText, NumberStyles.None, CultureInfo.InvariantCulture, out var abandonedAfterSeconds) ||
                abandonedAfterSeconds <= 0 || abandonedAfterSeconds > MaxAbandonedAfterSeconds)
            {
                return false;
            }
            return now - lastSeen > TimeSpan.FromSeconds(abandonedAfterSeconds);
        }

        // Deletes the abandoned queues of the service's other buses, unsubscribing event queues from the service's topic
        // first so it stops sending to them. Other buses may sweep at the same time: what one finds gone, another deleted.
        public static async Task SweepAsync(IAmazonSQS sqs, IAmazonSimpleNotificationService sns, string serviceName, string ownInstanceId, string topicArn, CancellationToken ct)
        {
            await SweepAsync(sqs, sns, serviceName, ownInstanceId, $"{serviceName}-events-", topicArn, ct).ConfigureAwait(false);
            await SweepAsync(sqs, sns, serviceName, ownInstanceId, $"{serviceName}-responses-", null, ct).ConfigureAwait(false);
        }

        private static async Task SweepAsync(IAmazonSQS sqs, IAmazonSimpleNotificationService sns, string serviceName, string ownInstanceId, string queueNamePrefix,
            string subscribedToTopicArn, CancellationToken ct)
        {
            foreach (var queueUrl in await QueueUrlsAsync(sqs, queueNamePrefix, ct).ConfigureAwait(false))
            {
                try
                {
                    if (!await IsAbandonedAsync(sqs, queueUrl, serviceName, ownInstanceId, ct).ConfigureAwait(false))
                    {
                        continue;
                    }
                    if (subscribedToTopicArn != null)
                    {
                        var queueArn = await AmazonSqsEntities.QueueArnAsync(sqs, queueUrl, ct).ConfigureAwait(false);
                        await UnsubscribeAsync(sns, subscribedToTopicArn, queueArn, ct).ConfigureAwait(false);
                    }

                    // Look again just before deleting, in case the bus has come back since. If it has, it subscribes
                    // again when it next refreshes.
                    if (await IsAbandonedAsync(sqs, queueUrl, serviceName, ownInstanceId, ct).ConfigureAwait(false))
                    {
                        await sqs.DeleteQueueAsync(queueUrl, ct).ConfigureAwait(false);
                    }
                }
                catch (QueueDoesNotExistException)
                {
                }
                catch (NotFoundException)
                {
                }
            }
        }

        private static async Task<bool> IsAbandonedAsync(IAmazonSQS sqs, string queueUrl, string serviceName, string ownInstanceId, CancellationToken ct)
        {
            var tags = (await sqs.ListQueueTagsAsync(new ListQueueTagsRequest { QueueUrl = queueUrl }, ct).ConfigureAwait(false)).Tags;
            var queueName = queueUrl.Substring(queueUrl.LastIndexOf('/') + 1);
            return tags != null && IsAbandoned(queueName, tags, serviceName, ownInstanceId, DateTimeOffset.UtcNow);
        }

        private static async Task<List<string>> QueueUrlsAsync(IAmazonSQS sqs, string queueNamePrefix, CancellationToken ct)
        {
            var queueUrls = new List<string>();
            string nextToken = null;
            do
            {
                var page = await sqs.ListQueuesAsync(new ListQueuesRequest { QueueNamePrefix = queueNamePrefix, MaxResults = MaxQueuesPerPage, NextToken = nextToken }, ct).ConfigureAwait(false);
                queueUrls.AddRange(page.QueueUrls ?? new List<string>());
                nextToken = page.NextToken;
            }
            while (!string.IsNullOrEmpty(nextToken));
            return queueUrls;
        }

        private static async Task UnsubscribeAsync(IAmazonSimpleNotificationService sns, string topicArn, string queueArn, CancellationToken ct)
        {
            string nextToken = null;
            do
            {
                var page = await sns.ListSubscriptionsByTopicAsync(new ListSubscriptionsByTopicRequest { TopicArn = topicArn, NextToken = nextToken }, ct).ConfigureAwait(false);
                foreach (var subscription in page.Subscriptions ?? new List<Subscription>())
                {
                    if (subscription.Endpoint == queueArn)
                    {
                        try
                        {
                            await sns.UnsubscribeAsync(subscription.SubscriptionArn, ct).ConfigureAwait(false);
                        }
                        catch (NotFoundException)
                        {
                        }
                    }
                }
                nextToken = page.NextToken;
            }
            while (!string.IsNullOrEmpty(nextToken));
        }
    }
}
