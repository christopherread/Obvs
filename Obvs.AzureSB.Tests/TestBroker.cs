using System;
using Azure.Messaging.ServiceBus.Administration;

namespace Obvs.AzureSB.Tests
{
    internal static class TestBroker
    {
        private const string Emulator = "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";

        // The Service Bus emulator from Emulator/docker-compose.yml. Set AZURE_SERVICE_BUS_CONNECTION_STRING to use another namespace.
        public static string ConnectionString => Environment.GetEnvironmentVariable("AZURE_SERVICE_BUS_CONNECTION_STRING") ?? Emulator;

        // The emulator serves management operations on port 5300
        public static ServiceBusAdministrationClient CreateAdministrationClient()
        {
            return new ServiceBusAdministrationClient(ConnectionString.Replace("Endpoint=sb://localhost;", "Endpoint=sb://localhost:5300;"));
        }
    }
}
