using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Obvs.Hosting;

namespace Obvs.Kafka.Configuration
{
    internal static class KafkaTopics
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

        // Metadata is fetched in short waits, so a refused sign-in fails at once rather than after the whole timeout
        private static readonly TimeSpan MetadataPollTimeout = TimeSpan.FromSeconds(1);

        // Consumers don't create topics, and one that subscribes before its topic exists can take minutes to
        // notice it, missing what's published meanwhile. So create any missing topics, with the broker's default
        // partitions and replication, and wait until each partition has a leader. Existing topics are left alone,
        // so this works without permission to create topics as long as they already exist.
        public static void EnsureCreated(string serviceName, KafkaConfiguration kafkaConfiguration, IReadOnlyCollection<string> topics)
        {
            Error signInError = null;
            var builder = new AdminClientBuilder(AdminClientConfig(kafkaConfiguration))
                .SetErrorHandler((_, error) =>
                {
                    if (error.Code == ErrorCode.Local_Authentication || error.Code == ErrorCode.Local_Ssl)
                    {
                        Volatile.Write(ref signInError, error);
                    }
                });
            if (kafkaConfiguration.OAuthBearerTokenRefreshHandler != null)
            {
                builder.SetOAuthBearerTokenRefreshHandler(kafkaConfiguration.OAuthBearerTokenRefreshHandler);
            }

            using (var admin = builder.Build())
            {
                var deadline = DateTime.UtcNow + Timeout;
                bool IsReady(string topic) => KafkaTopics.IsReady(admin, topic, deadline, () => Volatile.Read(ref signInError), serviceName, kafkaConfiguration);

                var missing = topics.Where(topic => !IsReady(topic)).ToList();
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

                deadline = DateTime.UtcNow + Timeout;
                while (missing.Any(topic => !IsReady(topic)))
                {
                    if (DateTime.UtcNow > deadline)
                    {
                        throw new TimeoutException($"Kafka topics {string.Join(", ", missing)} weren't ready on {kafkaConfiguration.BootstrapServers} within {Timeout.TotalSeconds} seconds.");
                    }
                    Thread.Sleep(50);
                }
            }
        }

        internal static AdminClientConfig AdminClientConfig(KafkaConfiguration kafkaConfiguration)
        {
            var config = new AdminClientConfig { BootstrapServers = kafkaConfiguration.BootstrapServers };
            kafkaConfiguration.ApplyTo(config);
            return config;
        }

        internal static string SignInFailure(string serviceName, KafkaConfiguration kafkaConfiguration, Error error)
        {
            var check = error.Code == ErrorCode.Local_Ssl
                ? "Check its SslCaLocation or SslCaPem, and SslCertificateLocation and SslKeyLocation if the brokers authenticate clients by certificate."
                : kafkaConfiguration.OAuthBearerTokenRefreshHandler != null
                    ? "Check its OAuthBearerTokenProvider, and that the token it gets is for these brokers."
                    : "Check its SaslUsername and SaslPassword, and its SaslMechanism.";
            return $"Service '{serviceName}' couldn't connect to Kafka at {kafkaConfiguration.BootstrapServers}: {error.Reason}. {check}";
        }

        private static bool IsReady(IAdminClient admin, string topic, DateTime deadline, Func<Error> signInError, string serviceName, KafkaConfiguration kafkaConfiguration)
        {
            Metadata metadata;
            while (true)
            {
                var error = signInError();
                if (error != null)
                {
                    throw new ObvsConfigurationException(new[] { SignInFailure(serviceName, kafkaConfiguration, error) });
                }

                try
                {
                    metadata = admin.GetMetadata(topic, MetadataPollTimeout);
                    break;
                }
                catch (KafkaException exception) when (exception.Error.IsLocalError && DateTime.UtcNow < deadline)
                {
                    // Not connected yet
                }
            }

            var topicMetadata = metadata.Topics.SingleOrDefault(t => t.Topic == topic);
            return topicMetadata != null
                && topicMetadata.Error.Code == ErrorCode.NoError
                && topicMetadata.Partitions.Count > 0
                && topicMetadata.Partitions.All(partition => partition.Leader >= 0);
        }
    }
}
