using System;
using Obvs.ActiveMQ.Configuration;
using Obvs.Configuration;
using Obvs.Serialization.Json.Configuration;
using Obvs.Types;

namespace Obvs.ActiveMQ.Test.Service
{
    public interface IMyServiceMessage : IMessage { }
    public class MyCommand : IMyServiceMessage, ICommand
    {
        public string Data { get; set; }
        public override string ToString()
        {
            return Data;
        }
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
            IServiceBus serviceBus = ServiceBus.Configure()
                .WithActiveMQEndpoints<IMyServiceMessage>()
                    .Named("MyService")
                    .UsingQueueFor<ICommand>()
                    .ConnectToBroker("tcp://192.168.99.100:32780") // local docker port for 61616
                    .WithCredentials("TESTUSER", "testpassword")
                    .SerializedAsJson()
                    .AsServer()
                .Create();

            serviceBus.Commands.Subscribe(async c =>
            {
                Console.WriteLine("Received command " + c);
                await serviceBus.PublishAsync(new MyEvent());
            });

            Console.WriteLine("Hit <Enter> to exit program.");
            Console.ReadLine();

        }
    }
}
