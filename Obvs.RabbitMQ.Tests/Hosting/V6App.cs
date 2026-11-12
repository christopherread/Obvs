using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Obvs.RabbitMQ.Tests.Hosting
{
    // Obvs.V6InteropApp, a v6 app on the published 6.x packages, run in its own process as a real v6 app would be.
    //   server <service> <broker>           answers PlaceOrder with OrderPlaced and GetQuote with a Quote of 9.99,
    //                                       prints "ready <Obvs version>" once subscribed, and exits when stdin closes
    //   send <service> <broker> <orderId>   sends PlaceOrder, prints the OrderId of the OrderPlaced that answers it, and exits
    public sealed class V6App : IAsyncDisposable
    {
        private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(30);

        private readonly Process _process;
        private readonly BlockingCollection<string> _output = new BlockingCollection<string>();
        private readonly ConcurrentQueue<string> _errors = new ConcurrentQueue<string>();

        private V6App(string arguments)
        {
            if (!File.Exists(Path))
            {
                throw new InvalidOperationException($"There's no v6 interop app at {Path}. Build Obvs.V6InteropApp, which the test project references.");
            }
            _process = Process.Start(new ProcessStartInfo("dotnet", $"\"{Path}\" {arguments}")
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            _process.OutputDataReceived += (sender, args) =>
            {
                if (args.Data == null) _output.CompleteAdding();
                else _output.Add(args.Data);
            };
            _process.ErrorDataReceived += (sender, args) => { if (args.Data != null) _errors.Enqueue(args.Data); };
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
        }

        // The Obvs version the app runs on, as it reported when it started
        public string ObvsVersion { get; private set; }

        private static string Path => typeof(V6App).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == "V6InteropApp").Value;

        public static Task<V6App> StartServerAsync(string service, string broker, CancellationToken cancellationToken)
        {
            var app = new V6App($"server {service} {broker}");
            return Task.Run(() =>
            {
                var ready = app.ReadLine(StartTimeout, cancellationToken).Split(' ');
                if (ready[0] != "ready")
                {
                    throw app.Failed($"The v6 server started with '{string.Join(" ", ready)}' rather than 'ready'.");
                }
                app.ObvsVersion = ready[1];
                return app;
            }, cancellationToken);
        }

        // Returns the OrderId of the OrderPlaced event the v6 client received
        public static async Task<string> SendCommandAsync(string service, string broker, string orderId, TimeSpan timeout, CancellationToken cancellationToken)
        {
            await using var app = new V6App($"send {service} {broker} {orderId}");
            return await Task.Run(() => app.ReadLine(timeout, cancellationToken), cancellationToken);
        }

        private string ReadLine(TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (_output.TryTake(out var line, (int)timeout.TotalMilliseconds, cancellationToken))
            {
                return line;
            }
            throw Failed(_output.IsAddingCompleted ? "The v6 app exited without answering." : $"The v6 app didn't answer within {timeout}.");
        }

        private Exception Failed(string message)
        {
            return new InvalidOperationException($"{message} Its errors:{Environment.NewLine}{string.Join(Environment.NewLine, _errors)}");
        }

        public async ValueTask DisposeAsync()
        {
            if (!_process.HasExited)
            {
                // The server exits when its input closes
                _process.StandardInput.Close();
                using var exited = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                try
                {
                    await _process.WaitForExitAsync(exited.Token);
                }
                catch (OperationCanceledException)
                {
                    _process.Kill(entireProcessTree: true);
                }
            }
            _process.Dispose();
        }
    }
}
