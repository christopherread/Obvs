using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Obvs.Transports;

namespace Obvs.Hosting
{
    // Settles messages whose transport attached an IMessageSettlement; others need nothing. A failure to settle is
    // logged rather than thrown, so it doesn't stop later messages being handled.
    internal static class Settlement
    {
        public static Task CompleteAsync(object message, string serviceName, ILogger logger)
        {
            return SettleAsync(message, settlement => settlement.CompleteAsync(), serviceName, logger);
        }

        public static Task AbandonAsync(object message, Exception exception, string serviceName, ILogger logger)
        {
            return SettleAsync(message, settlement => settlement.AbandonAsync(exception), serviceName, logger);
        }

        public static Task ReleaseAsync(object message, string serviceName, ILogger logger)
        {
            return SettleAsync(message, settlement => settlement.ReleaseAsync(), serviceName, logger);
        }

        private static async Task SettleAsync(object message, Func<IMessageSettlement, Task> settle, string serviceName, ILogger logger)
        {
            var settlement = ObvsReceivedMessage.GetSettlement(message);
            if (settlement == null)
            {
                return;
            }

            try
            {
                await settle(settlement).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Couldn't settle {MessageType} on service '{ServiceName}' with its transport.", message.GetType().Name, serviceName);
            }
        }
    }
}
