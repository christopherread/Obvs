using System;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Obvs.ActiveMQ.Configuration;
using Obvs.Configuration;
using Obvs.Example.Messages;
using Obvs.Serialization.Json.Configuration;
using Obvs.Types;

namespace Obvs.Example.Service.ActiveMQ
{
    public class MyService : IHostedService, IDisposable
    {
        private IDisposable _subscription;
        private string _brokerUri;
        private string _serviceName;

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            // delay startup to let broker start first
            var delay = TimeSpan.FromSeconds(10); 
            Console.WriteLine($"Starting in {delay}");
            await Task.Delay(delay, cancellationToken);

            // get env variables
            _brokerUri = Environment.GetEnvironmentVariable("ACTIVEMQ_BROKER_URI") ?? "tcp://localhost:61616";
            _serviceName = Environment.GetEnvironmentVariable("OBVS_SERVICE_NAME") ?? "MyService";

            Console.WriteLine($"Starting {_serviceName}, connecting to broker {_brokerUri}");

            // create singleton ServiceBus
            var serviceBus = ServiceBus.Configure()
                .WithActiveMQEndpoints<IServiceMessage1>() // you can use different transports for each service
                    .Named(_serviceName) // used to prefix AMQ topic names
                    .UsingQueueFor<ICommand>() // optional, default is topic
                    .ConnectToBroker(_brokerUri)
                    .WithCredentials("admin", "admin")
                    .SerializedAsJson() // you can use different serialization formats for each service
                    .AsServer() // will subscribe to commands and requests, and publish events and responses
                .UsingConsoleLogging() // optional logging extensions available
                .Create();

            // subscribe to commands and publish events

            _subscription = serviceBus.Commands.OfType<Command1>().Subscribe(async cmd =>
            {
                await serviceBus.PublishAsync(new Event1 { Data = cmd.Data });
            });
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine($"Stopping service {_serviceName}");

            _subscription?.Dispose();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            Console.WriteLine($"Disposing service {_serviceName}");

            _subscription?.Dispose();
        }
    }

    internal static class Program
    {
        private static async Task Main(string[] args)
        {
            var hostBuilder = new HostBuilder()
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddSingleton<IHostedService, MyService>();
                });

            await hostBuilder.RunConsoleAsync();
        }
    }
}
