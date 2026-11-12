using System;

namespace Obvs.NATS.Tests
{
    internal static class TestBroker
    {
        // Set NATS_URL to run the Explicit tests against a server other than a default local install
        public static string Url => Environment.GetEnvironmentVariable("NATS_URL") ?? "nats://localhost:4222";

        // A server with JetStream enabled, for example nats-server -js -p 4223. Set NATS_JETSTREAM_URL to use another.
        public static string JetStreamUrl => Environment.GetEnvironmentVariable("NATS_JETSTREAM_URL") ?? "nats://localhost:4223";
    }
}
