using System;

namespace Obvs.Testing
{
    /// <summary>
    /// Thrown when an expected message didn't arrive in time. The message lists what did pass through
    /// the broker, which usually shows what went wrong.
    /// </summary>
    public sealed class ObvsTestTimeoutException : TimeoutException
    {
        public ObvsTestTimeoutException(string message)
            : base(message)
        {
        }
    }
}
