using System;
using System.Threading;
using Apache.NMS;

namespace Obvs.ActiveMQ.Configuration
{
    internal static class ConnectionFactoryExtensions
    {
        public static Lazy<IConnection> CreateLazyConnection(this IConnectionFactory connectionFactory, string userName = null, string password = null)
        {
            return new Lazy<IConnection>(() =>
            {
                var connection = !string.IsNullOrEmpty(userName)
                    ? connectionFactory.CreateConnection(userName, password) 
                    : connectionFactory.CreateConnection();
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