using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using Amazon.SQS.Model;

namespace Obvs.AmazonSqs
{
    // A service's queues and topic, and this bus's own: the queue the topic delivers its events to and the queue it
    // receives replies on. SQS names allow letters, digits, hyphens and underscores, so names are joined with hyphens.
    internal sealed class AmazonSqsEntities
    {
        // Tags on this bus's own queues, so ones a crashed bus left behind can be found
        public const string InstanceIdTag = "obvs-instance-id";
        public const string ServiceTag = "obvs-service";

        // Dead letters are kept for the longest SQS allows, 14 days
        private static readonly string DeadLetterRetentionSeconds = ((int)TimeSpan.FromDays(14).TotalSeconds).ToString(CultureInfo.InvariantCulture);

        // SQS refuses to create a queue for 60 seconds after one of the same name was deleted, as when a bus with a
        // fixed instance ID restarts
        private static readonly TimeSpan RecreateRetryDelay = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan RecreateRetryLimit = TimeSpan.FromSeconds(70);

        private readonly string _serviceName;
        private readonly string _instanceId;
        private readonly AmazonSqsOptions _options;
        private string _deadLettersArn;
        private string _subscriptionArn;

        public AmazonSqsEntities(string serviceName, string instanceId, AmazonSqsOptions options)
        {
            _serviceName = serviceName;
            _instanceId = instanceId;
            _options = options;
            Commands = $"{serviceName}-commands";
            Requests = $"{serviceName}-requests";
            DeadLetters = $"{serviceName}-dead-letters";
            Events = $"{serviceName}-events";
            EventQueue = $"{serviceName}-events-{instanceId}";
            Replies = $"{serviceName}-responses-{instanceId}";
        }

        public string Commands { get; }
        public string Requests { get; }
        public string DeadLetters { get; }
        public string Events { get; }
        public string EventQueue { get; }
        public string Replies { get; }

        // Set once the entities exist
        public string CommandsUrl { get; private set; }
        public string RequestsUrl { get; private set; }
        public string DeadLettersUrl { get; private set; }
        public string EventsTopicArn { get; private set; }
        public string EventQueueUrl { get; private set; }
        public string RepliesUrl { get; private set; }

        // The names of every queue a bus of the service may use
        public static IEnumerable<string> QueueNames(string serviceName, string instanceId)
        {
            var entities = new AmazonSqsEntities(serviceName, instanceId, null);
            return new[] { entities.Commands, entities.Requests, entities.DeadLetters, entities.EventQueue, entities.Replies };
        }

        public static IAmazonSQS CreateSqsClient(AmazonSqsOptions options)
        {
            var config = new AmazonSQSConfig();
            if (string.IsNullOrWhiteSpace(options.ServiceUrl))
            {
                config.RegionEndpoint = RegionEndpoint.GetBySystemName(options.Region);
            }
            else
            {
                config.ServiceURL = options.ServiceUrl;
                config.AuthenticationRegion = options.Region;
            }
            return options.Credentials != null ? new AmazonSQSClient(options.Credentials, config) : new AmazonSQSClient(config);
        }

        public static IAmazonSimpleNotificationService CreateSnsClient(AmazonSqsOptions options)
        {
            var config = new AmazonSimpleNotificationServiceConfig();
            if (string.IsNullOrWhiteSpace(options.ServiceUrl))
            {
                config.RegionEndpoint = RegionEndpoint.GetBySystemName(options.Region);
            }
            else
            {
                config.ServiceURL = options.ServiceUrl;
                config.AuthenticationRegion = options.Region;
            }
            return options.Credentials != null ? new AmazonSimpleNotificationServiceClient(options.Credentials, config) : new AmazonSimpleNotificationServiceClient(config);
        }

        // Creates the entities the bus needs that don't exist yet; another bus of the service may create them at the
        // same time. Settings apply only to queues Obvs creates; existing ones keep theirs.
        public async Task EnsureCreatedAsync(bool includeBusEntities, CancellationToken ct = default)
        {
            using (var sqs = CreateSqsClient(_options))
            using (var sns = CreateSnsClient(_options))
            {
                DeadLettersUrl = await EnsureQueueAsync(sqs, DeadLetters, new Dictionary<string, string> { [QueueAttributeName.MessageRetentionPeriod] = DeadLetterRetentionSeconds }, null, ct).ConfigureAwait(false);
                _deadLettersArn = await QueueArnAsync(sqs, DeadLettersUrl, ct).ConfigureAwait(false);
                CommandsUrl = await EnsureQueueAsync(sqs, Commands, QueueAttributes(_deadLettersArn), null, ct).ConfigureAwait(false);
                RequestsUrl = await EnsureQueueAsync(sqs, Requests, QueueAttributes(_deadLettersArn), null, ct).ConfigureAwait(false);
                EventsTopicArn = (await sns.CreateTopicAsync(new CreateTopicRequest { Name = Events }, ct).ConfigureAwait(false)).TopicArn;

                if (includeBusEntities)
                {
                    await EnsureBusEntitiesAsync(sqs, sns, ct).ConfigureAwait(false);
                }
            }
        }

        // Records that the bus is still running, recreating its own queues and subscription if another bus took it for
        // crashed and deleted them, for example while it was paused
        public async Task RefreshBusEntitiesAsync(CancellationToken ct)
        {
            using (var sqs = CreateSqsClient(_options))
            using (var sns = CreateSnsClient(_options))
            {
                await EnsureBusEntitiesAsync(sqs, sns, ct).ConfigureAwait(false);
            }
        }

        // Deletes the queues and subscriptions crashed buses of the service left behind
        public async Task SweepAbandonedAsync(CancellationToken ct)
        {
            using (var sqs = CreateSqsClient(_options))
            using (var sns = CreateSnsClient(_options))
            {
                await AbandonedQueues.SweepAsync(sqs, sns, _serviceName, _instanceId, EventsTopicArn, ct).ConfigureAwait(false);
            }
        }

        // Tags the queues whether this bus created them or found them, as when it restarts with a fixed instance ID, so
        // they record when it was last seen from now on, or no longer do if it has clean-up turned off. Subscribing
        // again returns the subscription already there.
        private async Task EnsureBusEntitiesAsync(IAmazonSQS sqs, IAmazonSimpleNotificationService sns, CancellationToken ct)
        {
            var tags = new Dictionary<string, string> { [InstanceIdTag] = _instanceId, [ServiceTag] = _serviceName };
            if (_options.CleanUpAbandonedQueues)
            {
                foreach (var tag in AbandonedQueues.LastSeenTags(_options, DateTimeOffset.UtcNow))
                {
                    tags[tag.Key] = tag.Value;
                }
            }
            RepliesUrl = await EnsureQueueAsync(sqs, Replies, QueueAttributes(_deadLettersArn), tags, ct).ConfigureAwait(false);
            EventQueueUrl = await EnsureQueueAsync(sqs, EventQueue, QueueAttributes(_deadLettersArn), tags, ct).ConfigureAwait(false);
            foreach (var queueUrl in new[] { RepliesUrl, EventQueueUrl })
            {
                await sqs.TagQueueAsync(new TagQueueRequest { QueueUrl = queueUrl, Tags = tags }, ct).ConfigureAwait(false);
                if (!_options.CleanUpAbandonedQueues)
                {
                    await sqs.UntagQueueAsync(new UntagQueueRequest { QueueUrl = queueUrl, TagKeys = new List<string> { AbandonedQueues.LastSeenTag, AbandonedQueues.AbandonedAfterTag } }, ct).ConfigureAwait(false);
                }
            }
            var eventQueueArn = await QueueArnAsync(sqs, EventQueueUrl, ct).ConfigureAwait(false);

            // The topic may send to the queue, and delivers each event's body and attributes as published
            await sqs.SetQueueAttributesAsync(new SetQueueAttributesRequest
            {
                QueueUrl = EventQueueUrl,
                Attributes = new Dictionary<string, string> { [QueueAttributeName.Policy] = TopicMaySendPolicy(eventQueueArn, EventsTopicArn) },
            }, ct).ConfigureAwait(false);
            _subscriptionArn = (await sns.SubscribeAsync(new SubscribeRequest
            {
                TopicArn = EventsTopicArn,
                Protocol = "sqs",
                Endpoint = eventQueueArn,
                Attributes = new Dictionary<string, string> { ["RawMessageDelivery"] = "true" },
                ReturnSubscriptionArn = true,
            }, ct).ConfigureAwait(false)).SubscriptionArn;
        }

        // The bus's own queues, and its subscription to the service's topic
        public async Task DeleteBusEntitiesAsync()
        {
            using (var sqs = CreateSqsClient(_options))
            using (var sns = CreateSnsClient(_options))
            {
                if (_subscriptionArn != null)
                {
                    await IgnoreIfMissing(() => sns.UnsubscribeAsync(_subscriptionArn)).ConfigureAwait(false);
                }
                if (EventQueueUrl != null)
                {
                    await IgnoreIfMissing(() => sqs.DeleteQueueAsync(EventQueueUrl)).ConfigureAwait(false);
                }
                if (RepliesUrl != null)
                {
                    await IgnoreIfMissing(() => sqs.DeleteQueueAsync(RepliesUrl)).ConfigureAwait(false);
                }
            }
        }

        // Received messages stay hidden for the visibility timeout, and move to the dead-letter queue once received
        // more than MaxDeliveryAttempts times
        private Dictionary<string, string> QueueAttributes(string deadLettersArn)
        {
            var redrivePolicy = JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["deadLetterTargetArn"] = deadLettersArn,
                ["maxReceiveCount"] = _options.MaxDeliveryAttempts,
            });
            return new Dictionary<string, string>
            {
                [QueueAttributeName.VisibilityTimeout] = VisibilityTimeoutSeconds(_options).ToString(CultureInfo.InvariantCulture),
                [QueueAttributeName.RedrivePolicy] = redrivePolicy,
            };
        }

        // SQS takes whole seconds
        public static int VisibilityTimeoutSeconds(AmazonSqsOptions options)
        {
            return (int)Math.Ceiling(options.VisibilityTimeout.TotalSeconds);
        }

        private static async Task<string> EnsureQueueAsync(IAmazonSQS sqs, string name, Dictionary<string, string> attributes, Dictionary<string, string> tags, CancellationToken ct)
        {
            try
            {
                return (await sqs.GetQueueUrlAsync(name, ct).ConfigureAwait(false)).QueueUrl;
            }
            catch (QueueDoesNotExistException)
            {
            }

            var deadline = DateTime.UtcNow + RecreateRetryLimit;
            while (true)
            {
                try
                {
                    return (await sqs.CreateQueueAsync(new CreateQueueRequest { QueueName = name, Attributes = attributes, Tags = tags }, ct).ConfigureAwait(false)).QueueUrl;
                }
                catch (QueueNameExistsException)
                {
                    // Another bus created it at the same time, with other settings
                    return (await sqs.GetQueueUrlAsync(name, ct).ConfigureAwait(false)).QueueUrl;
                }
                catch (QueueDeletedRecentlyException) when (DateTime.UtcNow < deadline)
                {
                    await Task.Delay(RecreateRetryDelay, ct).ConfigureAwait(false);
                }
            }
        }

        public static async Task<string> QueueArnAsync(IAmazonSQS sqs, string queueUrl, CancellationToken ct)
        {
            var response = await sqs.GetQueueAttributesAsync(new GetQueueAttributesRequest
            {
                QueueUrl = queueUrl,
                AttributeNames = new List<string> { QueueAttributeName.QueueArn },
            }, ct).ConfigureAwait(false);
            return response.Attributes[QueueAttributeName.QueueArn];
        }

        private static string TopicMaySendPolicy(string queueArn, string topicArn)
        {
            return JsonSerializer.Serialize(new
            {
                Version = "2012-10-17",
                Statement = new[]
                {
                    new
                    {
                        Effect = "Allow",
                        Principal = new { Service = "sns.amazonaws.com" },
                        Action = "sqs:SendMessage",
                        Resource = queueArn,
                        Condition = new { ArnEquals = new Dictionary<string, string> { ["aws:SourceArn"] = topicArn } },
                    },
                },
            });
        }

        private static async Task IgnoreIfMissing(Func<Task> delete)
        {
            try
            {
                await delete().ConfigureAwait(false);
            }
            catch (QueueDoesNotExistException)
            {
            }
            catch (Amazon.SimpleNotificationService.Model.NotFoundException)
            {
            }
        }
    }
}
