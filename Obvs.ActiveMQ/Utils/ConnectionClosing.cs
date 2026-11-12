using System;
using System.Runtime.CompilerServices;
using Apache.NMS;

namespace Obvs.ActiveMQ.Utils
{
    // NMS's Connection.Close holds its list of sessions locked while it waits for each session to shut down, and a
    // session closing on another thread needs that lock to leave the list, so the two deadlock. Closing a connection
    // and its sessions under one lock per connection keeps them apart.
    internal static class ConnectionClosing
    {
        private static readonly ConditionalWeakTable<IConnection, object> Gates = new ConditionalWeakTable<IConnection, object>();

        public static void Close(IConnection connection, Action close)
        {
            lock (Gates.GetValue(connection, _ => new object()))
            {
                close();
            }
        }
    }
}
