using System;
using System.Collections.Generic;
using Xunit;

namespace Obvs.AmazonSqs.Tests
{
    // Which of a service's queues a running bus treats as left behind by a crashed one; the Explicit tests in Hosting
    // check the sweep against LocalStack
    public class TestAbandonedQueues
    {
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

        [Fact]
        public void ShouldTagWhenTheBusWasLastSeenAndHowLongItMayGoUnseen()
        {
            var options = new AmazonSqsOptions { AbandonedAfter = TimeSpan.FromMinutes(90) };

            var tags = AbandonedQueues.LastSeenTags(options, Now);

            Assert.Equal("2026-10-26T12:00:00Z", tags[AbandonedQueues.LastSeenTag]);
            Assert.Equal("5400", tags[AbandonedQueues.AbandonedAfterTag]);
        }

        [Fact]
        public void ShouldRoundTheWindowUpToWholeSeconds()
        {
            var options = new AmazonSqsOptions { AbandonedAfter = TimeSpan.FromMilliseconds(2500) };

            Assert.Equal("3", AbandonedQueues.LastSeenTags(options, Now)[AbandonedQueues.AbandonedAfterTag]);
        }

        [Theory]
        [InlineData("orders-events-worker-2")]
        [InlineData("orders-responses-worker-2")]
        public void ShouldTreatABusesQueueNotSeenWithinItsWindowAsAbandoned(string queueName)
        {
            Assert.True(AbandonedQueues.IsAbandoned(queueName, Tags("worker-2", lastSeen: Now.AddMinutes(-61), abandonedAfterSeconds: "3600"), "orders", "worker-1", Now));
        }

        [Fact]
        public void ShouldKeepABusesQueueSeenWithinItsWindow()
        {
            Assert.False(AbandonedQueues.IsAbandoned("orders-events-worker-2", Tags("worker-2", lastSeen: Now.AddMinutes(-59), abandonedAfterSeconds: "3600"), "orders", "worker-1", Now));
        }

        [Fact]
        public void ShouldGoByTheWindowTheQueuesOwnBusTaggedItWith()
        {
            // Buses of one service may be configured differently; each is judged by its own interval, not the sweeper's
            Assert.False(AbandonedQueues.IsAbandoned("orders-events-worker-2", Tags("worker-2", lastSeen: Now.AddHours(-2), abandonedAfterSeconds: "10800"), "orders", "worker-1", Now));
            Assert.True(AbandonedQueues.IsAbandoned("orders-events-worker-2", Tags("worker-2", lastSeen: Now.AddMinutes(-11), abandonedAfterSeconds: "600"), "orders", "worker-1", Now));
        }

        [Fact]
        public void ShouldKeepAQueueWithoutWhenItWasLastSeen()
        {
            // A bus with clean-up turned off doesn't record it, so other buses leave its queues alone
            var tags = Tags("worker-2", lastSeen: Now.AddDays(-30), abandonedAfterSeconds: "3600");
            tags.Remove(AbandonedQueues.LastSeenTag);

            Assert.False(AbandonedQueues.IsAbandoned("orders-events-worker-2", tags, "orders", "worker-1", Now));
        }

        [Fact]
        public void ShouldKeepAQueueWithoutAWindow()
        {
            var tags = Tags("worker-2", lastSeen: Now.AddDays(-30), abandonedAfterSeconds: "3600");
            tags.Remove(AbandonedQueues.AbandonedAfterTag);

            Assert.False(AbandonedQueues.IsAbandoned("orders-events-worker-2", tags, "orders", "worker-1", Now));
        }

        [Theory]
        [InlineData("yesterday", "3600")]
        [InlineData("2026-10-25T12:00:00Z", "an hour")]
        [InlineData("2026-10-25T12:00:00Z", "0")]
        [InlineData("2026-10-25T12:00:00Z", "-60")]
        public void ShouldKeepAQueueWhoseTagsCantBeRead(string lastSeen, string abandonedAfterSeconds)
        {
            var tags = Tags("worker-2", lastSeen: Now, abandonedAfterSeconds: abandonedAfterSeconds);
            tags[AbandonedQueues.LastSeenTag] = lastSeen;

            Assert.False(AbandonedQueues.IsAbandoned("orders-events-worker-2", tags, "orders", "worker-1", Now));
        }

        [Fact]
        public void ShouldKeepThisBusesOwnQueues()
        {
            Assert.False(AbandonedQueues.IsAbandoned("orders-events-worker-1", Tags("worker-1", lastSeen: Now.AddDays(-1), abandonedAfterSeconds: "3600"), "orders", "worker-1", Now));
        }

        [Fact]
        public void ShouldKeepAnotherServicesQueues()
        {
            // The service orders-events-x has a queue whose name starts like one of this service's own
            var tags = Tags("worker-2", lastSeen: Now.AddDays(-1), abandonedAfterSeconds: "3600");
            tags[AmazonSqsEntities.ServiceTag] = "orders-events-x";

            Assert.False(AbandonedQueues.IsAbandoned("orders-events-x-commands", tags, "orders", "worker-1", Now));
            Assert.False(AbandonedQueues.IsAbandoned("orders-events-worker-2", tags, "orders", "worker-1", Now));
        }

        [Theory]
        [InlineData("orders-commands")]
        [InlineData("orders-dead-letters")]
        [InlineData("orders-events-worker-3")]
        public void ShouldKeepQueuesThatArentTheTaggedBusesOwn(string queueName)
        {
            Assert.False(AbandonedQueues.IsAbandoned(queueName, Tags("worker-2", lastSeen: Now.AddDays(-1), abandonedAfterSeconds: "3600"), "orders", "worker-1", Now));
        }

        private static Dictionary<string, string> Tags(string instanceId, DateTimeOffset lastSeen, string abandonedAfterSeconds)
        {
            return new Dictionary<string, string>
            {
                [AmazonSqsEntities.InstanceIdTag] = instanceId,
                [AmazonSqsEntities.ServiceTag] = "orders",
                [AbandonedQueues.LastSeenTag] = lastSeen.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                [AbandonedQueues.AbandonedAfterTag] = abandonedAfterSeconds,
            };
        }
    }
}
