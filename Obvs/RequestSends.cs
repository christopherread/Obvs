using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Obvs
{
    // Passes a request's send from the ServiceEndpointClient that starts it to the telemetry a few wrappers out, which
    // can't otherwise tell when it's sent: GetResponses's stream carries only responses and errors. Subscribing runs
    // through the wrappers synchronously, so the send starts on the thread that's watching.
    internal static class RequestSends
    {
        [ThreadStatic]
        private static StrongBox<Task> _watching;

        // Runs subscribe, and returns the send it started, or a completed task if the endpoint client didn't say
        public static IDisposable Watch(Func<IDisposable> subscribe, out Task sent)
        {
            var outer = _watching;
            var watching = _watching = new StrongBox<Task>();
            try
            {
                var subscription = subscribe();
                sent = watching.Value ?? Task.CompletedTask;
                return subscription;
            }
            finally
            {
                _watching = outer;
            }
        }

        public static void Started(Task send)
        {
            if (_watching != null)
            {
                _watching.Value = send;
            }
        }
    }
}
