using System;

namespace Obvs.NATS.Tests
{
    internal static class TestBroker
    {
        // Set NATS_URL to run the Explicit tests against a server other than a default local install
        public static string Url => Environment.GetEnvironmentVariable("NATS_URL") ?? "nats://localhost:4222";
    }
}
