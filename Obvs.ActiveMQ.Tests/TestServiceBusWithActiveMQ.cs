using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using NUnit.Framework;
using Obvs.ActiveMQ.Configuration;
using Obvs.Serialization.Json.Configuration;
using Obvs.Types;

namespace Obvs.ActiveMQ.Tests
{
    [TestFixture]
    public class TestServiceBusWithActiveMQ
    {
        [Test, Explicit]
        public void TestServiceEndpointsOverAmqBroker()
        {
            // set up ServiceBus using fluent interfaces and all current endpoints and pointing at test AMQ broker
            IServiceBus serviceBus = ServiceBus.Configure()
                .WithActiveMQEndpoints<ITestMessage>()
                    .Named("Obvs.TestService")
                    .UsingQueueFor<ICommand>()
                    .ConnectToBroker("tcp://localhost:61616")
                    .SerializedAsJson()
                    .AsClientAndServer()
                .Create();

            // create threadsafe collection to hold received messages in
            ConcurrentBag<IMessage> messages = new ConcurrentBag<IMessage>();

            // create some actions that will act as a fake services acting on incoming commands and requests
            Action<TestCommand> fakeService1 = command => serviceBus.Publish(new TestEvent {Id = command.Id});
            Action<TestRequest> fakeService2 = request => serviceBus.Reply(request, new TestResponse {Id = request.Id});
            AnonymousObserver<IMessage> observer = new AnonymousObserver<IMessage>(msg => { messages.Add(msg); Console.WriteLine(msg); }, exception => Console.WriteLine(exception));

            // subscribe to all messages on the ServiceBus
            serviceBus.Events.Subscribe(observer);
            serviceBus.Commands.Subscribe(observer);
            serviceBus.Requests.Subscribe(observer);
            serviceBus.Commands.OfType<TestCommand>().Subscribe(fakeService1);
            serviceBus.Requests.OfType<TestRequest>().Subscribe(fakeService2);

            // send some messages
            serviceBus.Send(new TestCommand { Id = 123 });
            serviceBus.GetResponses(new TestRequest { Id = 456 }).Subscribe(observer);

            // wait some time until we think all messages have been sent and received over AMQ
            Thread.Sleep(TimeSpan.FromSeconds(3));

            // test we got everything we expected
            Assert.That(messages.OfType<TestCommand>().Count() == 1, "TestCommand not received");
            Assert.That(messages.OfType<TestEvent>().Count() == 1, "TestEvent not received");
            Assert.That(messages.OfType<TestRequest>().Count() == 1, "TestRequest not received");
            Assert.That(messages.OfType<TestResponse>().Count() == 1, "TestResponse not received");

            // win!
        }

        public interface ITestMessage : IMessage
        {
        }

        public class TestEvent : ITestMessage, IEvent
        {
            public int Id { get; set; }

            public override string ToString()
            {
                return "TestEvent " + Id;
            }
        }

        public class TestCommand : ITestMessage, ICommand
        {
            public int Id { get; set; }

            public override string ToString()
            {
                return "TestCommand " + Id;
            }
        }

        public class TestRequest : ITestMessage, IRequest
        {
            public int Id { get; set; }

            public override string ToString()
            {
                return "TestRequest " + Id;
            }

            public string RequestId { get; set; }
            public string RequesterId { get; set; }
        }

        public class TestResponse : ITestMessage, IResponse
        {
            public int Id { get; set; }

            public override string ToString()
            {
                return "TestResponse " + Id;
            }

            public string RequestId { get; set; }
            public string RequesterId { get; set; }
        }

    }
}