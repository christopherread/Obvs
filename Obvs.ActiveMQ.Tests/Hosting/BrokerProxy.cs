using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Obvs.ActiveMQ.Tests.Hosting
{
    // Stands in for a broker that isn't up yet: a port nothing listens on until Start, which forwards it to the test broker
    internal sealed class BrokerProxy : IDisposable
    {
        private readonly int _port;
        private readonly Uri _broker;
        private readonly CancellationTokenSource _stop = new CancellationTokenSource();
        private readonly ConcurrentBag<TcpClient> _clients = new ConcurrentBag<TcpClient>();
        private TcpListener _listener;

        public BrokerProxy(string brokerUri)
        {
            _broker = new Uri(brokerUri.StartsWith("activemq:", StringComparison.Ordinal) ? brokerUri.Substring("activemq:".Length) : brokerUri);
            var free = new TcpListener(IPAddress.Loopback, 0);
            free.Start();
            _port = ((IPEndPoint)free.LocalEndpoint).Port;
            free.Stop();
        }

        public string Uri => $"activemq:tcp://127.0.0.1:{_port}";

        public void Start()
        {
            _listener = new TcpListener(IPAddress.Loopback, _port);
            _listener.Start();
            _ = Task.Run(AcceptAsync);
        }

        private async Task AcceptAsync()
        {
            while (!_stop.IsCancellationRequested)
            {
                TcpClient accepted;
                try
                {
                    accepted = await _listener.AcceptTcpClientAsync(_stop.Token);
                }
                catch (Exception) when (_stop.IsCancellationRequested)
                {
                    return;
                }
                _ = Task.Run(() => ForwardAsync(accepted));
            }
        }

        private async Task ForwardAsync(TcpClient accepted)
        {
            var broker = new TcpClient();
            _clients.Add(accepted);
            _clients.Add(broker);
            try
            {
                await broker.ConnectAsync(_broker.Host, _broker.Port, _stop.Token);
                var fromClient = accepted.GetStream().CopyToAsync(broker.GetStream(), _stop.Token);
                var fromBroker = broker.GetStream().CopyToAsync(accepted.GetStream(), _stop.Token);
                await Task.WhenAny(fromClient, fromBroker);
            }
            catch (Exception)
            {
                // Either end closing ends the connection
            }
            finally
            {
                accepted.Dispose();
                broker.Dispose();
            }
        }

        public void Dispose()
        {
            _stop.Cancel();
            _listener?.Stop();
            foreach (var client in _clients)
            {
                client.Dispose();
            }
        }
    }
}
