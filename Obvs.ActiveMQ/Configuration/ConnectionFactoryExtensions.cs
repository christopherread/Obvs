using System;
using System.Threading;
using Apache.NMS;

namespace Obvs.ActiveMQ.Configuration
{
    internal static class ConnectionFactoryExtensions
    {
        // Connects on first use, and again on the next use after a failure, as when the service starts before its broker:
        // PublicationOnly doesn't keep the exception, as ExecutionAndPublication would. It can run the factory on more than
        // one thread at once, so the lock keeps it to one connection.
        public static Lazy<IConnection> CreateLazyConnection(this IConnectionFactory connectionFactory, string userName = null, string password = null)
        {
            var gate = new object();
            IConnection connected = null;
            return new Lazy<IConnection>(() =>
            {
                lock (gate)
                {
                    if (connected == null)
                    {
                        var connection = !string.IsNullOrEmpty(userName)
                            ? connectionFactory.CreateConnection(userName, password)
                            : connectionFactory.CreateConnection();
                        try
                        {
                            connection.Start();
                        }
                        catch
                        {
                            connection.Dispose();
                            throw;
                        }
                        connected = connection;
                    }
                    return connected;
                }
            }, LazyThreadSafetyMode.PublicationOnly);
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