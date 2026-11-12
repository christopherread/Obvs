using System;

namespace Obvs.Kafka.Tests
{
    internal static class TestBroker
    {
        // Set KAFKA_BOOTSTRAP_SERVERS to run the Explicit tests against a broker other than a default local install
        public static string BootstrapServers => Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP_SERVERS") ?? "localhost:9092";

        // The broker that requires SASL sign-in, from SaslBroker/docker-compose.yml, and its test user
        public static string SaslBootstrapServers => Environment.GetEnvironmentVariable("KAFKA_SASL_BOOTSTRAP_SERVERS") ?? "127.0.0.1:9094";
        public const string SaslUsername = "obvs";
        public const string SaslPassword = "obvs-secret";
    }
}
