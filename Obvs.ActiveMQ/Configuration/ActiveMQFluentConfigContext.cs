using System;
using Apache.NMS;

namespace Obvs.ActiveMQ.Configuration
{
    internal static class ActiveMQFluentConfigContext
    {
        [ThreadStatic]
        internal static Lazy<IConnection> SharedConnection;   
    }
}