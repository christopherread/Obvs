using System;

namespace Obvs.RabbitMQ.Tests
{
    internal static class TestBroker
    {
        // Set RABBITMQ_URI to run the Explicit tests against a broker other than a default local install
        public static string Uri => Environment.GetEnvironmentVariable("RABBITMQ_URI") ?? "amqp://localhost:5672";
    }
}
