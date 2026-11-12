using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Apache.NMS.ActiveMQ;
using Obvs.ActiveMQ.Utils;
using Xunit;

namespace Obvs.ActiveMQ.Tests
{
    // NMS deadlocks when a session closes on one thread while its connection closes on another: one subscriber
    // stopping while another thread disposes the bus, say
    public class TestConnectionClosing
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldNotDeadlockWhenSessionsCloseWhileTheirConnectionCloses()
        {
            for (var attempt = 0; attempt < 50; attempt++)
            {
                var connection = new ConnectionFactory(TestBroker.Uri).CreateConnection();
                connection.Start();
                var sessions = Enumerable.Range(0, 10).Select(_ => connection.CreateSession()).ToArray();
                // A consumer each, as a message source has, which makes closing a session take longer
                foreach (var session in sessions)
                {
                    session.CreateConsumer(session.GetTopic("obvs-activemq-closing"));
                }
                using var start = new Barrier(sessions.Length + 1);

                Task CloseTogether(Action close) => Task.Factory.StartNew(() =>
                {
                    start.SignalAndWait();
                    ConnectionClosing.Close(connection, close);
                }, Ct, TaskCreationOptions.LongRunning, TaskScheduler.Default);

                var closing = sessions
                    .Select(session => CloseTogether(() =>
                    {
                        session.Close();
                        session.Dispose();
                    }))
                    .Append(CloseTogether(() =>
                    {
                        connection.Close();
                        connection.Dispose();
                    }));

                await Task.WhenAll(closing).WaitAsync(TimeSpan.FromSeconds(15), Ct);
            }
        }
    }
}
