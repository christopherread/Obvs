using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Obvs.NATS.Tests.JetStream
{
    // A nats-server of the test's own, which it can stop and start again on the same port and storage directory, so streams
    // and consumers survive as they would a real restart. Runs nats-server from the PATH; set NATS_SERVER to use another.
    public sealed class RestartableJetStreamServer : IAsyncDisposable
    {
        private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(10);

        // Below the ports the OS gives outgoing connections (from 32768 on Linux, 49152 on macOS and Windows), which could
        // take a server's port while it's down, or before it first starts. Servers of tests running at the same time can
        // still pick the same one, so a server that can't listen on its port picks another.
        private const int FirstPort = 20000;
        private const int LastPort = 32000;
        private const int Attempts = 10;

        private readonly string _storeDirectory = Path.Combine(Path.GetTempPath(), $"obvs-js-{Guid.NewGuid():N}");
        private int _port;
        private Process _process;

        private RestartableJetStreamServer()
        {
        }

        public string Url => $"nats://127.0.0.1:{_port}";

        private static string Executable => Environment.GetEnvironmentVariable("NATS_SERVER") ?? "nats-server";

        public static async Task<RestartableJetStreamServer> StartAsync()
        {
            var server = new RestartableJetStreamServer();
            for (var attempt = 1; ; attempt++)
            {
                server._port = Random.Shared.Next(FirstPort, LastPort);
                try
                {
                    await server.StartAgainAsync();
                    return server;
                }
                catch (PortInUseException) when (attempt < Attempts)
                {
                }
            }
        }

        // Without JetStream, the server is core NATS only
        public async Task StartAgainAsync(bool jetStream = true)
        {
            var arguments = jetStream ? $"-js -sd \"{_storeDirectory}\" -a 127.0.0.1 -p {_port}" : $"-a 127.0.0.1 -p {_port}";
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
            var output = new ConcurrentQueue<string>();
            var ready = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            DataReceivedEventHandler record = (sender, args) =>
            {
                if (args.Data == null)
                {
                    // It's exited, and everything it wrote has been read
                    ready.TrySetResult(false);
                    return;
                }
                Record(output, args.Data);
                if (args.Data.Contains("Server is ready"))
                {
                    ready.TrySetResult(true);
                }
            };
            _process.OutputDataReceived += record;
            _process.ErrorDataReceived += record;
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            bool started;
            try
            {
                started = await ready.Task.WaitAsync(StartTimeout);
            }
            catch (TimeoutException)
            {
                await StopAsync();
                throw new InvalidOperationException(
                    $"nats-server {arguments} didn't start in {StartTimeout}: {string.Join(Environment.NewLine, output)}");
            }
            if (!started)
            {
                // Waits until both its outputs have been read
                _process.WaitForExit();
                var written = string.Join(Environment.NewLine, output);
                await StopAsync();
                if (written.Contains("Error listening on port"))
                {
                    throw new PortInUseException();
                }
                throw new InvalidOperationException($"nats-server {arguments} exited: {written}");
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

        // The last lines it wrote, which say why it couldn't start
        private static void Record(ConcurrentQueue<string> output, string line)
        {
            output.Enqueue(line);
            while (output.Count > 20 && output.TryDequeue(out _))
            {
            }
        }

        private sealed class PortInUseException : Exception
        {
        }
    }
}
