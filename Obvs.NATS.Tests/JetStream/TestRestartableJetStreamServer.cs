using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NATS.Client.Core;
using Xunit;

namespace Obvs.NATS.Tests.JetStream
{
    // The tests' own nats-server, which test classes running at the same time each start. Runs by itself, as its load
    // would hold up other tests' acknowledgements.
    [Collection(nameof(TestRestartableJetStreamServer))]
    public class TestRestartableJetStreamServer
    {
        [CollectionDefinition(nameof(TestRestartableJetStreamServer), DisableParallelization = true)]
        public sealed class RunsAlone
        {
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldStartManyServersAtOnce()
        {
            // Other tests' connections are given ports from the range the OS gives outgoing connections
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            var accepting = Task.Run(async () =>
            {
                while (!stop.IsCancellationRequested)
                {
                    using var accepted = await listener.AcceptTcpClientAsync(stop.Token);
                }
            }, stop.Token);
            var connecting = Enumerable.Range(0, 16).Select(_ => Task.Run(async () =>
            {
                while (!stop.IsCancellationRequested)
                {
                    // Closed with a reset, so its port is free again at once, not kept for a while
                    using var client = new TcpClient { LingerState = new LingerOption(true, 0) };
                    await client.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port, stop.Token);
                }
            }, stop.Token)).ToArray();
            try
            {
                await StartManyServersAsync();
            }
            finally
            {
                stop.Cancel();
                listener.Stop();
                try
                {
                    await Task.WhenAll(connecting.Concat(new[] { accepting }));
                }
                catch (System.Exception)
                {
                }
            }
        }

        private static async Task StartManyServersAsync()
        {
            for (var round = 0; round < 10; round++)
            {
                var servers = await Task.WhenAll(Enumerable.Range(0, 30).Select(_ => Task.Run(RestartableJetStreamServer.StartAsync)));
                try
                {
                    Assert.Equal(servers.Length, servers.Select(server => server.Url).Distinct().Count());
                    await Task.WhenAll(servers.Select(async server =>
                    {
                        await using var connection = new NatsConnection(new NatsOpts { Url = server.Url });
                        await connection.ConnectAsync();
                    }));
                }
                finally
                {
                    await Task.WhenAll(servers.Select(server => server.DisposeAsync().AsTask()));
                }
            }
        }
    }
}
