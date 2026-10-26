using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Obvs
{
    /// <summary>
    /// How a transport settles a message it delivered, for transports whose broker keeps a message until
    /// it's settled. A message's handler settles it once it has run; messages no handler handles are
    /// completed when they arrive.
    /// </summary>
    public interface IMessageSettlement
    {
        /// <summary>The message was handled: remove it from the broker.</summary>
        Task CompleteAsync();

        /// <summary>
        /// The message's handler threw <paramref name="exception"/>: have the broker deliver it again, or
        /// dead-letter it once the transport's delivery limit is reached.
        /// </summary>
        Task AbandonAsync(Exception exception);

        /// <summary>The bus is stopping before the message was handled: give it back to the broker to deliver again.</summary>
        Task ReleaseAsync();
    }

    /// <summary>
    /// Attaches a transport's <see cref="IMessageSettlement"/> to a message it delivers, without it being part
    /// of the message. Messages without one are settled by their transport when they arrive.
    /// </summary>
    public static class ObvsMessageSettlement
    {
        // Keyed by the message instance, not its value, and weak, so a settlement goes when its message does
        private static readonly ConditionalWeakTable<object, IMessageSettlement> Settlements = new ConditionalWeakTable<object, IMessageSettlement>();

        /// <summary>Attaches <paramref name="settlement"/> to <paramref name="message"/>, replacing any already attached.</summary>
        public static void Set(object message, IMessageSettlement settlement)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }
            if (settlement == null)
            {
                throw new ArgumentNullException(nameof(settlement));
            }

            Settlements.Remove(message);
            Settlements.Add(message, settlement);
        }

        /// <summary>The settlement attached to <paramref name="message"/>, or null if its transport settles it on arrival.</summary>
        public static IMessageSettlement Get(object message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            return Settlements.TryGetValue(message, out var settlement) ? settlement : null;
        }
    }
}
