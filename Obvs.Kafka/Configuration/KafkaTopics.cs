using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Confluent.Kafka;
using Confluent.Kafka.Admin;

namespace Obvs.Kafka.Configuration
{
    internal static class KafkaTopics
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

        // Consumers don't create topics, and one that subscribes before its topic exists can take minutes to
        // notice it, missing what's published meanwhile. So create any missing topics, with the broker's default
        // partitions and replication, and wait until each partition has a leader. Existing topics are left alone,
        // so this works without permission to create topics as long as they already exist.
        public static void EnsureCreated(KafkaConfiguration kafkaConfiguration, IReadOnlyCollection<string> topics)
        {
            using (var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = kafkaConfiguration.BootstrapServers }).Build())
            {
                var missing = topics.Where(topic => !IsReady(admin, topic)).ToList();
                if (missing.Count == 0)
                {
                    return;
                }

                try
                {
                    admin.CreateTopicsAsync(missing.Select(topic => new TopicSpecification { Name = topic, NumPartitions = -1, ReplicationFactor = -1 }))
                        .GetAwaiter().GetResult();
                }
                catch (CreateTopicsException exception) when (exception.Results.All(result => result.Error.Code == ErrorCode.NoError || result.Error.Code == ErrorCode.TopicAlreadyExists))
                {
                    // Another bus created them first
                }

                var deadline = DateTime.UtcNow + Timeout;
                while (missing.Any(topic => !IsReady(admin, topic)))
                {
                    if (DateTime.UtcNow > deadline)
                    {
                        throw new TimeoutException($"Kafka topics {string.Join(", ", missing)} weren't ready on {kafkaConfiguration.BootstrapServers} within {Timeout.TotalSeconds} seconds.");
                    }
                    Thread.Sleep(50);
                }
            }
        }

        private static bool IsReady(IAdminClient admin, string topic)
        {
            var metadata = admin.GetMetadata(topic, Timeout).Topics.SingleOrDefault(t => t.Topic == topic);
            return metadata != null
                && metadata.Error.Code == ErrorCode.NoError
                && metadata.Partitions.Count > 0
                && metadata.Partitions.All(partition => partition.Leader >= 0);
        }
    }
}
