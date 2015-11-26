using System;
using System.Threading;
using Apache.NMS;

namespace Obvs.ActiveMQ.Configuration
{
    internal static class ConnectionFactoryExtensions
    {
        public static Lazy<IConnection> GetLazyConnection(this IConnectionFactory connectionFactory)
        {
            return new Lazy<IConnection>(() =>
            {
                IConnection connection = connectionFactory.CreateConnection();
                connection.Start();
                return connection;
            }, LazyThreadSafetyMode.ExecutionAndPublication);
        }
        
        public static Lazy<IConnection> GetLazyConnection(this IConnection connection)
        {
            return new Lazy<IConnection>(() =>
            {
                connection.Start();
                return connection;
            }, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }
}