using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Obvs.NATS.Tests.JetStream
{
    // A nats-server of the test's own, which it can stop and start again on the same port and storage directory, so streams
    // and consumers survive as they would a real restart. Runs nats-server from the PATH; set NATS_SERVER to use another.
    public sealed class RestartableJetStreamServer : IAsyncDisposable
    {
        private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(10);

        private readonly int _port = FreePort();
        private readonly string _storeDirectory = Path.Combine(Path.GetTempPath(), $"obvs-js-{Guid.NewGuid():N}");
        private Process _process;

        private RestartableJetStreamServer()
        {
        }

        public string Url => $"nats://127.0.0.1:{_port}";

        private static string Executable => Environment.GetEnvironmentVariable("NATS_SERVER") ?? "nats-server";

        public static async Task<RestartableJetStreamServer> StartAsync()
        {
            var server = new RestartableJetStreamServer();
            await server.StartAgainAsync();
            return server;
        }

        // Without JetStream, the server is core NATS only
        public async Task StartAgainAsync(bool jetStream = true)
        {
            var arguments = jetStream ? $"-js -sd \"{_storeDirectory}\" -p {_port}" : $"-p {_port}";
            var startInfo = new ProcessStartInfo(Executable, arguments)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            try
            {
                _process = Process.Start(startInfo);
            }
            catch (Win32Exception exception)
            {
                throw new InvalidOperationException(
                    $"Couldn't run '{Executable}'. Install nats-server and put it on the PATH, or set NATS_SERVER to its path.", exception);
            }
            _process.OutputDataReceived += (sender, args) => { };
            _process.ErrorDataReceived += (sender, args) => { };
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            var deadline = DateTime.UtcNow + StartTimeout;
            while (true)
            {
                if (_process.HasExited)
                {
                    throw new InvalidOperationException($"nats-server {arguments} exited with code {_process.ExitCode}.");
                }
                try
                {
                    using var client = new TcpClient();
                    await client.ConnectAsync(IPAddress.Loopback, _port);
                    return;
                }
                catch (SocketException) when (DateTime.UtcNow < deadline)
                {
                    await Task.Delay(50);
                }
            }
        }

        // Kills the server, as a crash or lost connection would
        public async Task StopAsync()
        {
            if (_process == null)
            {
                return;
            }
            if (!_process.HasExited)
            {
                _process.Kill();
            }
            await _process.WaitForExitAsync();
            _process.Dispose();
            _process = null;
        }

        public async Task RestartAsync(TimeSpan downFor = default)
        {
            await StopAsync();
            await Task.Delay(downFor);
            await StartAgainAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync();
            if (Directory.Exists(_storeDirectory))
            {
                Directory.Delete(_storeDirectory, recursive: true);
            }
        }

        private static int FreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}
