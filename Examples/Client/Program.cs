using System;
using Obvs.Types;
using Obvs.ActiveMQ.Configuration;
using Obvs.Configuration;
using Obvs.Example.Messages;
using Obvs.Serialization.Json.Configuration;

namespace Obvs.Example.Client
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            var brokerUri = Environment.GetEnvironmentVariable("ACTIVEMQ_BROKER_URI") ?? "tcp://localhost:61616";
            var serviceName = Environment.GetEnvironmentVariable("OBVS_SERVICE_NAME") ?? "Service1";

            Console.WriteLine($"Starting {serviceName}, connecting to broker {brokerUri}");

            var serviceBus = ServiceBus.Configure()
                .WithActiveMQEndpoints<IServiceMessage1>()
                    .Named(serviceName)
                    .UsingQueueFor<ICommand>()
                    .ConnectToBroker(brokerUri) 
                    .WithCredentials("TESTUSER", "testpassword1")
                    .SerializedAsJson()
                    .AsClient()
                .CreateClient();

            serviceBus.Events.Subscribe(c => Console.WriteLine("Received an event!"));
            
            Console.WriteLine("Hit <Enter> to send a command.");
            while (true)
            {
                string data = Console.ReadLine();
                serviceBus.SendAsync(new Command1 { Data = data });
            }
        }
    }
}
