using System;
using System.Reactive.Concurrency;

namespace Obvs.Testing
{
    public sealed class ObvsTestHarnessOptions
    {
        /// <summary>How long <c>Sent</c>, <c>Published</c>, <c>Requested</c> and <c>Replied</c> wait when no timeout is passed. Five seconds by default.</summary>
        public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// The clock timeouts are measured on. Pass a <c>FakeTimeProvider</c> to test timeouts by advancing
        /// time instead of waiting.
        /// </summary>
        public TimeProvider TimeProvider { get; set; } = TimeProvider.System;

        /// <summary>
        /// Where the in-memory broker delivers messages. The task pool by default; pass a <c>TestScheduler</c>
        /// to control delivery and virtual time in the Rx streams.
        /// </summary>
        public IScheduler Scheduler { get; set; }
    }
}
