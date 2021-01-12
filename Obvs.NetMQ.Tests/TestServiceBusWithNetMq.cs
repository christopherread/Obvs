using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Obvs.Configuration;
using Obvs.NetMQ.Configuration;
using Obvs.Serialization.Json.Configuration;
using Obvs.Serialization.ProtoBuf.Configuration;
using Obvs.Types;
using ProtoBuf;
using Xunit;

namespace Obvs.NetMQ.Tests
{
    public class TestServiceBusWithNetMq
    {
        /// <summary>
        /// This test illustrates an example of creating a system with two services in one process
        /// and two client each in their own process, but there are many other possibilties!
        /// </summary>
        [Fact, Trait("Category", "Explicit")]
        public async Task TestServiceEndpointsOverLocalHostSockets()
        {
            // create a server that hosts endpoints for two services
            var serviceBus = ServiceBus.Configure()
                .WithNetMqEndpoints<ITestService1>()
                    .Named("Obvs.TestNetMqService1")
                    .BindToAddress("tcp://localhost")
                    .OnPort(5555)
                    .SerializedAsJson() // messages will be serialized as strings
                    .AsServer()
                .WithNetMqEndpoints<ITestService2>()
                    .Named("Obvs.TestNetMqService2")
                    .BindToAddress("tcp://localhost")
                    .OnPort(6555)
                    .SerializedAsProtoBuf() // messages will be serialized as binary
                    .AsServer()
                .UsingConsoleLogging() // useful for debugging, but check out other proper logging extensions
                .Create();

            // create a client that connects to both services
            var serviceBusClient1 = ServiceBus.Configure()
                .WithNetMqEndpoints<ITestService1>()
                    .Named("Obvs.TestNetMqService1")
                    .BindToAddress("tcp://localhost")
                    .OnPort(5555)
                    .SerializedAsJson()
                    .AsClient()
                .WithNetMqEndpoints<ITestService2>()
                    .Named("Obvs.TestNetMqService2")
                    .BindToAddress("tcp://localhost")
                    .OnPort(6555)
                    .SerializedAsProtoBuf()
                    .AsClient()
                .UsingConsoleLogging()
                .CreateClient();

            // create a second client which only connects to one of the services
            var serviceBusClient2 = ServiceBus.Configure()
                .WithNetMqEndpoints<ITestService1>()
                    .Named("Obvs.TestNetMqService1")
                    .BindToAddress("tcp://localhost")
                    .OnPort(5555)
                    .SerializedAsJson()
                    .AsClient()
                .UsingConsoleLogging()
                .CreateClient();

            // create action to record all observed messages so we can assert later
            var messages = new ConcurrentBag<IMessage>();
            Action<IMessage> messageRecorder = msg => messages.Add(msg);
            
            // create some actions that will act as a fake services acting on incoming commands and requests
            Action<ITestService1> fakeService1 = msg =>
            {
                messageRecorder(msg);
                Console.WriteLine("Service1 received: {0}", msg);
                var command = msg as TestCommand1;
                if (command != null)
                {
                    serviceBus.PublishAsync(new TestEvent1 {Id = command.Id});
                }
                var request = msg as TestRequest1;
                if (request != null)
                {
                    serviceBus.ReplyAsync(request, new TestResponse1 { Id = request.Id });
                }
            };
            Action<ITestService2> fakeService2 = msg =>
            {
                messageRecorder(msg);
                Console.WriteLine("Service2 received: {0}", msg);
                var command = msg as TestCommand2;
                if (command != null)
                {
                    serviceBus.PublishAsync(new TestEvent2 { Id = command.Id });
                }
                var request = msg as TestRequest2;
                if (request != null)
                {
                    serviceBus.ReplyAsync(request, new TestResponse2 { Id = request.Id });
                }

            };

            // create some actions that will act as simple clients which display events and responses received
            Action<IMessage> fakeClient1 = msg =>
            {
                messageRecorder(msg);
                Console.WriteLine("Client1 received: {0}", msg);
            };
            Action<IMessage> fakeClient2 = msg =>
            {
                messageRecorder(msg);
                Console.WriteLine("Client2 received: {0}", msg);
            };
            
            // subscribe to events on clients
            serviceBusClient1.Events.Subscribe(fakeClient1);
            serviceBusClient2.Events.Subscribe(fakeClient2);

            // subscribe to commands and requests on server
            // each ObserveOn creates a queue and can be a very useful way of dispatching messages
            serviceBus.Commands.OfType<ITestService1>().ObserveOn(Scheduler.Default).Subscribe(fakeService1);
            serviceBus.Requests.OfType<ITestService1>().ObserveOn(Scheduler.Default).Subscribe(fakeService1);
            serviceBus.Commands.OfType<ITestService2>().ObserveOn(Scheduler.Default).Subscribe(fakeService2);
            serviceBus.Requests.OfType<ITestService2>().ObserveOn(Scheduler.Default).Subscribe(fakeService2);

            // send some messages from client1
            await serviceBusClient1.SendAsync(new TestCommand1 { Id = 1 });
            await serviceBusClient1.SendAsync(new TestCommand2 { Id = 3 });
            serviceBusClient1.GetResponses(new TestRequest1 { Id = 2 }).Subscribe(fakeClient1);
            serviceBusClient1.GetResponses(new TestRequest2 { Id = 4 }).Subscribe(fakeClient1);

            // send some messages from client2
            await serviceBusClient2.SendAsync(new TestCommand1 { Id = 5 });
            serviceBusClient2.GetResponses(new TestRequest1 { Id = 6 }).Subscribe(fakeClient1);

            // wait some time until we think all messages have been sent and received
            await Task.Delay(TimeSpan.FromSeconds(3));

            // test we got everything we expected
            Assert.True(messages.OfType<TestCommand1>().Count() == 2, "TestCommand1");
            Assert.True(messages.OfType<TestEvent1>().Count() == 4, "TestEvent1");

            Assert.True(messages.OfType<TestCommand2>().Count() == 1, "TestCommand2");
            Assert.True(messages.OfType<TestEvent2>().Count() == 1, "TestEvent2");

            Assert.True(messages.OfType<TestRequest1>().Count() == 2, "TestRequest1");
            Assert.True(messages.OfType<TestResponse1>().Count() == 2, "TestResponse1");

            Assert.True(messages.OfType<TestRequest2>().Count() == 1, "TestRequest2");
            Assert.True(messages.OfType<TestResponse2>().Count() == 1, "TestResponse2");

            // win!
        }

        public interface ITestService1 : IMessage { }

        public class TestEvent1 : ITestService1, IEvent
        {
            public int Id { get; set; }

            public override string ToString()
            {
                return string.Format("TestEvent1[Id={0}]", Id);
            }
        }

        public class TestCommand1 : ITestService1, ICommand
        {
            public int Id { get; set; }

            public override string ToString()
            {
                return string.Format("TestCommand1[Id={0}]", Id);
            }
        }

        public class TestRequest1 : ITestService1, IRequest
        {
            public int Id { get; set; }

            public override string ToString()
            {
                return string.Format("TestRequest1[Id={0}]", Id);
            }

            public string RequestId { get; set; }
            public string RequesterId { get; set; }
        }

        public class TestResponse1 : ITestService1, IResponse
        {
            public int Id { get; set; }

            public override string ToString()
            {
                return string.Format("TestResponse1[Id={0}]", Id);
            }

            public string RequestId { get; set; }
            public string RequesterId { get; set; }
        }
    
        public interface ITestService2 : IMessage { }

        [ProtoContract]
        public class TestEvent2 : ITestService2, IEvent
        {
            [ProtoMember(1)]
            public int Id { get; set; }

            public override string ToString()
            {
                return string.Format("TestEvent2[Id={0}]", Id);
            }
        }

        [ProtoContract]
        public class TestCommand2 : ITestService2, ICommand
        {
            [ProtoMember(1)]
            public int Id { get; set; }

            public override string ToString()
            {
                return string.Format("TestCommand2[Id={0}]", Id);
            }
        }

        [ProtoContract]
        public class TestRequest2 : ITestService2, IRequest
        {
            [ProtoMember(1)]
            public int Id { get; set; }

            public override string ToString()
            {
                return string.Format("TestRequest2[Id={0}]", Id);
            }

            [ProtoMember(2)]
            public string RequestId { get; set; }
            [ProtoMember(3)]
            public string RequesterId { get; set; }
        }

        [ProtoContract]
        public class TestResponse2 : ITestService2, IResponse
        {
            [ProtoMember(1)]
            public int Id { get; set; }

            public override string ToString()
            {
                return string.Format("TestResponse2[Id={0}]", Id);
            }

            [ProtoMember(2)]
            public string RequestId { get; set; }
            [ProtoMember(3)]
            public string RequesterId { get; set; }
        }
    }
}