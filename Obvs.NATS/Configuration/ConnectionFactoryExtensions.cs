using System;
using System.Threading;
using NATS.Client;

namespace Obvs.NATS.Configuration
{
    internal static class ConnectionFactoryExtensions
    {
        public static Lazy<IConnection> GetLazyConnection(this ConnectionFactory factory, string uri)
        {
            return new Lazy<IConnection>(() => factory.CreateConnection(uri), LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }
}