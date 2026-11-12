using System;
using Azure.Messaging.ServiceBus.Administration;

namespace Obvs.AzureSB.Tests
{
    internal static class TestBroker
    {
        private const string Emulator = "Endpoint=sb://localhost:5673;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";

        // The Service Bus emulator from Emulator/docker-compose.yml. Set AZURE_SERVICE_BUS_CONNECTION_STRING to use another namespace.
        public static string ConnectionString => Environment.GetEnvironmentVariable("AZURE_SERVICE_BUS_CONNECTION_STRING") ?? Emulator;

        // Fully qualified: Obvs.ServiceBusClient would win, as these tests sit inside the Obvs namespace
        public static Azure.Messaging.ServiceBus.ServiceBusClient CreateClient()
        {
            return new Azure.Messaging.ServiceBus.ServiceBusClient(ConnectionString);
        }

        // The emulator serves management operations on port 5300
        public static ServiceBusAdministrationClient CreateAdministrationClient()
        {
            return new ServiceBusAdministrationClient(ConnectionString.Replace("Endpoint=sb://localhost:5673;", "Endpoint=sb://localhost:5300;"));
        }
    }
}
