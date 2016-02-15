using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Obvs.Configuration;
using Obvs.NetMQ.Configuration;
using Obvs.Serialization.Json.Configuration;
using Obvs.Types;

namespace Obvs.NetMQ.Tests
{
    [TestFixture]
    public class TestServiceBusWithNetMq
    {
        [Test, Explicit]
        public async void TestServiceEndpointsOverLocalHostSockets()
        {
            // set up ServiceBus using fluent interfaces and all current endpoints and pointing at localhost
            var serviceBus = ServiceBus.Configure()
                .WithNetMqEndpoints<ITestMessage>()
                    .Named("Obvs.TestNetMqService")
                    .BindToAddress("tcp://localhost")
                    .OnPort(5555)
                    .SerializedAsJson()
                    .AsClientAndServer()
                .UsingConsoleLogging()
                .Create();

            // create threadsafe collection to hold received messages in
            var messages = new ConcurrentBag<IMessage>();

            // create some actions that will act as a fake services acting on incoming commands and requests
            Action<TestCommand> fakeService1 = command => serviceBus.PublishAsync(new TestEvent { Id = command.Id });
            Action<TestRequest> fakeService2 = request => serviceBus.ReplyAsync(request, new TestResponse { Id = request.Id });
            var observer = new AnonymousObserver<IMessage>(msg =>
            {
                messages.Add(msg);
                Console.WriteLine(msg);
            }, exception => Console.WriteLine(exception));

            // subscribe to all messages on the ServiceBus
            serviceBus.Events.Subscribe(observer);
            serviceBus.Commands.Subscribe(observer);
            serviceBus.Requests.Subscribe(observer);
            serviceBus.Commands.OfType<TestCommand>().SubscribeOn(TaskPoolScheduler.Default).Subscribe(fakeService1);
            serviceBus.Requests.OfType<TestRequest>().Subscribe(fakeService2);

            // send some messages
            await serviceBus.SendAsync(new TestCommand { Id = 123 });
            serviceBus.GetResponses(new TestRequest { Id = 456 }).Subscribe(observer);

            // wait some time until we think all messages have been sent and received over AMQ
            await Task.Delay(TimeSpan.FromSeconds(2));

            // test we got everything we expected
            Assert.That(messages.OfType<TestCommand>().Count() == 1, "TestCommand not received");
            Assert.That(messages.OfType<TestEvent>().Count() == 1, "TestEvent not received");
            Assert.That(messages.OfType<TestRequest>().Count() == 1, "TestRequest not received");
            Assert.That(messages.OfType<TestResponse>().Count() == 1, "TestResponse not received");

            // win!
        }

        public interface ITestMessage : IMessage { }

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