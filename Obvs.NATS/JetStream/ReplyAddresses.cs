using System.Runtime.CompilerServices;

namespace Obvs.NATS.JetStream
{
    // The subject prefix each received request asked for its response to go to, kept for as long as the request is alive
    internal static class ReplyAddresses
    {
        private static readonly ConditionalWeakTable<object, string> Addresses = new ConditionalWeakTable<object, string>();

        public static void Set(object request, string replyTo)
        {
            Addresses.Remove(request);
            Addresses.Add(request, replyTo);
        }

        public static bool TryGet(object request, out string replyTo)
        {
            return Addresses.TryGetValue(request, out replyTo);
        }
    }
}
