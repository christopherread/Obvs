namespace Obvs.Mcp
{
    /// <summary>What the Obvs MCP server allows, and how much it remembers.</summary>
    public sealed class ObvsMcpOptions
    {
        /// <summary>
        /// Whether agents can send commands, publish events and make requests, as well as look. True by default: the
        /// server only runs in Development unless <see cref="AllowOutsideDevelopment"/> says otherwise.
        /// </summary>
        public bool AllowSending { get; set; } = true;

        /// <summary>
        /// Whether the server may run outside the Development environment. False by default: an agent that can send
        /// messages can do real work, so turn this on only for a shared test environment you're happy for it to change.
        /// </summary>
        public bool AllowOutsideDevelopment { get; set; }

        /// <summary>How many of the bus's recent messages <c>watch</c> can return. 200 by default.</summary>
        public int RecentMessages { get; set; } = 200;

        /// <summary>How many recent errors <c>recent_errors</c> can return. 50 by default.</summary>
        public int RecentErrors { get; set; } = 50;
    }
}
