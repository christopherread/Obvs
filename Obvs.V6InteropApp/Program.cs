using System;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Reflection;
using System.Threading.Tasks;
using Obvs;
using Obvs.Configuration;
using Obvs.RabbitMQ.Configuration;
using Obvs.Serialization.Json.Configuration;
using Obvs.Types;

namespace Obvs.RabbitMQ.Tests.Hosting
{
    // A v6 app for TestV6Interop, configured as v6 apps are, with the fluent ServiceBus.Configure() chain and Newtonsoft JSON.
    //   server <service> <broker>           answers PlaceOrder with OrderPlaced and GetQuote with a Quote of 9.99,
    //                                       prints "ready <Obvs version>" once subscribed, and exits when stdin closes
    //   send <service> <broker> <orderId>   sends PlaceOrder, prints the OrderId of the OrderPlaced that answers it, and exits
    public static class Program
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

        public static async Task<int> Main(string[] args)
        {
            switch (args.FirstOrDefault())
            {
                case "server" when args.Length == 3:
                    Serve(args[1], args[2]);
                    return 0;
                case "send" when args.Length == 4:
                    return await SendAsync(args[1], args[2], args[3]);
                default:
                    Console.Error.WriteLine("Usage: server <service> <broker> | send <service> <broker> <orderId>");
                    return 2;
            }
        }

        private static void Serve(string service, string broker)
        {
            var bus = Create(service, broker, server: true);
            using (bus.Commands.OfType<PlaceOrder>().Subscribe(command => Report(bus.PublishAsync(new OrderPlaced { OrderId = command.OrderId }))))
            using (bus.Requests.OfType<GetQuote>().Subscribe(request => Report(bus.ReplyAsync(request, new Quote { Price = 9.99m }))))
            {
                Console.WriteLine($"ready {ObvsVersion()}");
                Console.In.ReadToEnd();
            }
            ((IDisposable)bus).Dispose();
        }

        private static async Task<int> SendAsync(string service, string broker, string orderId)
        {
            var bus = Create(service, broker, server: false);
            try
            {
                var placed = bus.Events.OfType<OrderPlaced>().FirstAsync(placedOrder => placedOrder.OrderId == orderId).Timeout(Timeout).ToTask();
                await bus.SendAsync(new PlaceOrder { OrderId = orderId });
                Console.WriteLine((await placed).OrderId);
                return 0;
            }
            catch (TimeoutException)
            {
                Console.Error.WriteLine($"No OrderPlaced for {orderId} within {Timeout}.");
                return 1;
            }
            finally
            {
                ((IDisposable)bus).Dispose();
            }
        }

        private static IServiceBus Create(string service, string broker, bool server)
        {
            var endpoint = ServiceBus.Configure()
                .WithRabbitMQEndpoints<IOrderMessage>()
                    .Named(service)
                    .ConnectToBroker(broker)
                    .SerializedAsJson();
            return server ? endpoint.AsServer().Create() : endpoint.AsClient().Create();
        }

        private static async void Report(Task sending)
        {
            try
            {
                await sending;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
            }
        }

        private static string ObvsVersion()
        {
            var assembly = typeof(IServiceBus).Assembly;
            return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? assembly.GetName().Version.ToString();
        }
    }
}
