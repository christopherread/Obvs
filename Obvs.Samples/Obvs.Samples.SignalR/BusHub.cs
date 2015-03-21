using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR;
using Obvs.NetMQ.Configuration;
using Obvs.Samples.Messages;
using DateTime = System.DateTime;
using Obvs.Serialization.Json.Configuration;

namespace Obvs.Samples.SignalR
{
    public class BusHub : Hub
    {
        private static int commandCount = 1;
        private static int eventCount = 1;
        private static int requestCount = 1;
        static IServiceBus Bus;

        static BusHub()
        {
            Bus = ServiceBus.Configure()
             .WithNetMqEndpoints<ITestMessage>()
             .Named("Obvs.Samples.SignalR")
             .BindToAddress("tcp://localhost")
             .OnPort(3000)
             .SerializedAsJson()
             .FilterMessageTypeAssemblies("Obvs.Samples.Messages")
             .AsClientAndServer()
             .Create();
        }

        public override Task OnConnected()
        {
            Clients.Caller.busResponse("Setting up Subscriptions");
            var connectionId = Context.ConnectionId;

            Bus.Exceptions.Subscribe(ex =>
            {
                var clients = GlobalHost.ConnectionManager.GetHubContext<BusHub>().Clients;
                clients.Client(connectionId).busResponse(ex.Message);
            }
            );
            Bus.Events.Subscribe(e =>
            {
                var clients = GlobalHost.ConnectionManager.GetHubContext<BusHub>().Clients;
                clients.Client(connectionId).busResponse(string.Format("Proocessed event {0} published by {1} at {2}.", ((TestEvent)e).Id, connectionId, DateTime.Now.ToLocalTime()));
            });
            Bus.Commands.Subscribe(c =>
            {
                var clients = GlobalHost.ConnectionManager.GetHubContext<BusHub>().Clients;
                clients.Client(connectionId).busResponse(string.Format("Proocessed command {0} sent by {1} at {2}.", ((TestCommand)c).Id, connectionId, DateTime.Now.ToLocalTime()));
            });


            return base.OnConnected();

        }
        public void BusCommand()
        {
            Clients.Caller.busResponse("Processing the command " + commandCount);
            Bus.Send(new TestCommand() { Id = commandCount++ });


        }

        public void BusEvent()
        {
            Clients.Caller.busResponse("Processing the event " + eventCount);
            Bus.Publish(new TestEvent() { Id = eventCount++ });

        }
    }
}