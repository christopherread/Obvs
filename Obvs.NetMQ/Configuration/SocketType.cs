using NetMQ.Sockets;

// v6's NetMQ transport building blocks, which UseNetMq(...) is built on: its public members are exempt from the rule that every one is documented
#pragma warning disable CS1570, CS1572, CS1591

namespace Obvs.NetMQ.Configuration
{
    public enum SocketType
    {
         Server,
         Client
    }
}