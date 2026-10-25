using System;

namespace Obvs.Kafka.Tests
{
    internal static class TestBroker
    {
        // Set KAFKA_BOOTSTRAP_SERVERS to run the Explicit tests against a broker other than a default local install
        public static string BootstrapServers => Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP_SERVERS") ?? "localhost:9092";
    }
}
