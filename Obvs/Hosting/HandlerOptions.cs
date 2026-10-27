namespace Obvs.Hosting
{
    /// <summary>How a handler runs, set with the <c>configure</c> argument of <c>HandleCommand</c>, <c>HandleEvent</c> or <c>HandleRequest</c>.</summary>
    public sealed class HandlerOptions
    {
        /// <summary>
        /// How many messages the handler may process at once. One by default, so messages are handled
        /// one at a time, in the order they arrive.
        /// </summary>
        public int MaxConcurrency { get; set; } = 1;
    }
}
