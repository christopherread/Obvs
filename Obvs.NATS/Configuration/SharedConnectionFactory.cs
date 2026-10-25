using System.Collections.Generic;
using NATS.Client.Core;

namespace Obvs.NATS.Configuration
{
    internal static class SharedConnectionFactory
    {
        private static readonly Dictionary<string, INatsConnection> SharedConnections = new Dictionary<string, INatsConnection>();

        public static INatsConnection Get(string url, bool isShared)
        {
            if (!isShared)
            {
                return CreateConnection(url);
            }
            INatsConnection connection;
            if (!SharedConnections.TryGetValue(url, out connection))
            {
                connection = CreateConnection(url);
                SharedConnections.Add(url, connection);
            }
            return connection;
        }

        // A NatsConnection only connects when it's first used
        private static INatsConnection CreateConnection(string url)
        {
            return new NatsConnection(new NatsOpts { Url = url });
        }
    }
}
