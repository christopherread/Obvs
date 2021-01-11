using System;
using System.Collections.Generic;
using System.Threading;
using NATS.Client;

namespace Obvs.NATS.Configuration
{
    internal static class SharedConnectionFactory
    {
        private static readonly Dictionary<string, Lazy<IConnection>> SharedConnections = new Dictionary<string, Lazy<IConnection>>();

        public static Lazy<IConnection> Get(string url, bool isShared)
        {
            if (!isShared)
            {
                return CreateLazyConnection(url);
            }
            Lazy<IConnection> connection;
            if (!SharedConnections.TryGetValue(url, out connection))
            {
                connection = CreateLazyConnection(url);
                SharedConnections.Add(url, connection);
            }
            return connection;
        }

        private static Lazy<IConnection> CreateLazyConnection(string url)
        {
            var factory = new ConnectionFactory();
            return new Lazy<IConnection>(() => factory.CreateConnection(url), LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }
}