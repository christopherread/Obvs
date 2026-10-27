using System;
using Amazon.Runtime;

namespace Obvs.AmazonSqs
{
    /// <summary>
    /// A service's Amazon SQS and SNS settings. Each service has its own, as named options keyed by the service's
    /// name: <c>IOptionsMonitor&lt;AmazonSqsOptions&gt;.Get("orders")</c>. Set at least <see cref="Region"/>.
    /// </summary>
    public sealed class AmazonSqsOptions
    {
        /// <summary>The AWS region the service's queues and topic are in, for example <c>eu-west-1</c>.</summary>
        public string Region { get; set; }

        /// <summary>
        /// An endpoint to use instead of AWS's own for the region, for example <c>http://localhost:4566</c> for LocalStack.
        /// </summary>
        public string ServiceUrl { get; set; }

        /// <summary>
        /// How to sign in to AWS. The AWS SDK's default chain (environment variables, profiles, IAM roles and so on)
        /// when not set. Set it in code; it isn't read from configuration.
        /// </summary>
        public AWSCredentials Credentials { get; set; }

        /// <summary>
        /// How many times a message is delivered to a handler that keeps failing before it's moved to the service's
        /// dead-letter queue, between 1 and 1000. 10 by default. Applied to the redrive policy of queues Obvs creates.
        /// </summary>
        public int MaxDeliveryAttempts { get; set; } = 10;

        /// <summary>
        /// How many messages each queue delivers to this bus at once. 1 by default, so messages arrive one at a time.
        /// Raise it so handlers with a <c>MaxConcurrency</c> above 1 can run in parallel. Standard queues don't
        /// guarantee order either way.
        /// </summary>
        public int MaxConcurrentCalls { get; set; } = 1;

        /// <summary>
        /// How long a received message stays hidden from other receivers before SQS delivers it again, between 1 second
        /// and 12 hours. 1 minute by default. It must exceed <see cref="MaxHandlerDuration"/>. Applied to queues Obvs
        /// creates, and to each receive.
        /// </summary>
        public TimeSpan VisibilityTimeout { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// The longest a handler is expected to take with a message, 30 seconds by default. SQS doesn't know a handler
        /// is still running, so a message whose handler outlasts <see cref="VisibilityTimeout"/> is delivered again;
        /// startup checks that the visibility timeout exceeds this.
        /// </summary>
        public TimeSpan MaxHandlerDuration { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Whether buses delete the queues, and topic subscriptions, that crashed buses of the service left behind. True
        /// by default. Each bus records on its own queues when it was last seen, refreshing it every
        /// <see cref="LastSeenInterval"/>, and deletes other buses' queues not seen for their <see cref="AbandonedAfter"/>.
        /// A running bus recreates its queues if they go missing, for example after being paused for longer than that. When
        /// false, the bus neither records when it was last seen, so other buses leave its queues alone, nor cleans up.
        /// </summary>
        public bool CleanUpAbandonedQueues { get; set; } = true;

        /// <summary>
        /// How often the bus records on its own queues that it's still running, and looks for queues crashed buses left
        /// behind. 5 minutes by default.
        /// </summary>
        public TimeSpan LastSeenInterval { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// How long the bus's queues may go without it being seen before other buses delete them, at least three times
        /// <see cref="LastSeenInterval"/>. 1 hour by default. Recorded on the bus's own queues, so other buses go by this
        /// bus's setting rather than their own. Allow for clock differences between machines.
        /// </summary>
        public TimeSpan AbandonedAfter { get; set; } = TimeSpan.FromHours(1);
    }
}
