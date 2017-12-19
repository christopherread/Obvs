using System;
using Obvs.ActiveMQ.Configuration;
using Obvs.Configuration;
using Obvs.Serialization.Json.Configuration;
using Obvs.Types;

namespace Obvs.ActiveMQ.Test.Client
{
    public interface IMyServiceMessage : IMessage { }
    public class MyCommand : IMyServiceMessage, ICommand
    {
        public string Data { get; set; }
    }
    public class MyEvent : IMyServiceMessage, IEvent { }
    public class MyRequest : IMyServiceMessage, IRequest
    {
        public string RequestId { get; set; }
        public string RequesterId { get; set; }
    }
    public class MyResponse : IMyServiceMessage, IResponse
    {
        public string RequestId { get; set; }
        public string RequesterId { get; set; }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var serviceBus = ServiceBus.Configure()
                .WithActiveMQEndpoints<IMyServiceMessage>()
                    .Named("MyService")
                    .UsingQueueFor<ICommand>()
                    .ConnectToBroker("tcp://192.168.99.100:32780") // local docker port for 61616
                    .WithCredentials("TESTUSER", "testpassword1")
                    .SerializedAsJson()
                    .AsClient()
                .CreateClient();

            serviceBus.Events.Subscribe(c => Console.WriteLine("Received an event!"));
            
            Console.WriteLine("Hit <Enter> to send a command.");
            while (true)
            {
                string data = Console.ReadLine();
                serviceBus.SendAsync(new MyCommand { Data = data });
            }
        }
    }
}
