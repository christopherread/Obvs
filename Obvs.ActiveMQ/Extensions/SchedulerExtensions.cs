using System;
using System.Reactive.Concurrency;
using System.Threading.Tasks;

namespace Obvs.ActiveMQ.Extensions
{
    internal static class SchedulerExtensions
    {
        public static Task ScheduleAsync(this IScheduler scheduler, Action action)
        {
            var tcs = new TaskCompletionSource<bool>();

            scheduler.Schedule(() =>
            {
                try
                {
                    action();
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });

            return tcs.Task;
        }
    }
}