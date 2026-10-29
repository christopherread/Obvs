namespace Obvs.NetMQ
{
    /// <summary>
    /// A service's NetMQ settings. Each service has its own, as named options keyed by the service's
    /// name: <c>IOptionsMonitor&lt;NetMqOptions&gt;.Get("orders")</c>.
    /// </summary>
    public sealed class NetMqOptions
    {
        /// <summary>
        /// The address the service's server binds to and its clients connect to, without a port, for example
        /// <c>tcp://localhost</c>. Required.
        /// </summary>
        public string Address { get; set; }

        /// <summary>
        /// The first of the service's four ports: requests use <c>Port</c>, responses <c>Port + 1</c>, commands
        /// <c>Port + 2</c> and events <c>Port + 3</c>, as in v6. Required.
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// Sends each message's headers, such as its trace context, in a fifth frame. v6 apps can't read a message
        /// with one, so turn this on once every app on the service is on v7. Off by default. Messages with headers
        /// are read either way.
        /// </summary>
        public bool SendHeaders { get; set; }
    }
}
