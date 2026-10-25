using System;

namespace Obvs.ActiveMQ.Tests
{
    internal static class TestBroker
    {
        // Set ACTIVEMQ_URI to run the Explicit tests against a broker other than a default local install
        public static string Uri => Environment.GetEnvironmentVariable("ACTIVEMQ_URI") ?? "activemq:tcp://localhost:61616";
    }
}
