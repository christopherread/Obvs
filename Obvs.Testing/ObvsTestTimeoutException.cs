using System;

namespace Obvs.Testing
{
    /// <summary>
    /// Thrown when an expected message didn't arrive in time. The message lists what did pass through
    /// the broker, which usually shows what went wrong.
    /// </summary>
    public sealed class ObvsTestTimeoutException : TimeoutException
    {
        /// <summary>What wasn't seen in time, and what was.</summary>
        /// <param name="message">The message expected, and the messages that passed through the broker</param>
        public ObvsTestTimeoutException(string message)
            : base(message)
        {
        }
    }
}
