using NetMQ.Sockets;

namespace Obvs.NetMQ.Configuration
{
    /// <summary>Whether a NetMQ source or publisher binds its socket to the address, or connects it to one bound there.</summary>
    public enum SocketType
    {
        /// <summary>Binds to the address, which the other side connects to</summary>
        Server,

        /// <summary>Connects to the address, which the other side binds to</summary>
        Client
    }
}