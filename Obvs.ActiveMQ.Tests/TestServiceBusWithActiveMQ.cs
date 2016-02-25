using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using EmbedMq;
using NUnit.Framework;
using Obvs.ActiveMQ.Configuration;
using Obvs.Configuration;
using Obvs.Serialization.Json.Configuration;
using Obvs.Types;

namespace Obvs.ActiveMQ.Tests
{
    [TestFixture]
    public class TestServiceBusWithActiveMQ
    {
        private EmbeddedBroker _broker;

        [SetUp]
        public void SetUp()
        {
            _broker = new EmbeddedBroker();
            Console.WriteLine("Created broker: {0}", _broker.FailoverUri);
        }

        [TearDown]
        public void TearDown()
        {
           Console.WriteLine("Disposing broker: {0}", _broker.FailoverUri);
           _broker.Dispose();
        }

        [Test]
        public void TestServiceBusWithEmbeddedBroker()
        {
            // use the embedded broker
            var brokerUri = _broker.FailoverUri;

            // set up ServiceBus using fluent interfaces and all current endpoints and pointing at test AMQ broker
            IServiceBus serviceBus = ServiceBus.Configure()
                .WithActiveMQEndpoints<ITestMessage1>()
                    .Named("Obvs.TestService")
                    .UsingQueueFor<TestCommand>().ClientAcknowledge()
                    .UsingQueueFor<TestCommand2>().ClientAcknowledge()
                    .UsingQueueFor<IRequest>().AutoAcknowledge()
                    .ConnectToBroker(brokerUri)
                    .SerializedAsJson()
                    .AsClientAndServer()
                .PublishLocally()
                    .OnlyMessagesWithNoEndpoints()
                .UsingConsoleLogging()
                .Create();

            // create threadsafe collection to hold received messages in
            ConcurrentBag<IMessage> messages = new ConcurrentBag<IMessage>();

            // create some actions that will act as a fake services acting on incoming commands and requests
            Action<TestCommand> fakeService1 = command => serviceBus.PublishAsync(new TestEvent {Id = command.Id});
            Action<TestRequest> fakeService2 = request => serviceBus.ReplyAsync(request, new TestResponse {Id = request.Id});
            AnonymousObserver<IMessage> observer = new AnonymousObserver<IMessage>(messages.Add, Console.WriteLine, () => Console.WriteLine("OnCompleted"));

            // subscribe to all messages on the ServiceBus
            CompositeDisposable subscriptions = new CompositeDisposable
            {
                serviceBus.Events.Subscribe(observer),
                serviceBus.Commands.Subscribe(observer),
                serviceBus.Requests.Subscribe(observer),
                serviceBus.Commands.OfType<TestCommand>().Subscribe(fakeService1),
                serviceBus.Requests.OfType<TestRequest>().Subscribe(fakeService2)
            };
            
            // send some messages
            serviceBus.SendAsync(new TestCommand { Id = 123 });
            serviceBus.SendAsync(new TestCommand2 { Id = 123 });
            serviceBus.SendAsync(new TestCommand3 { Id = 123 });
            serviceBus.GetResponses(new TestRequest { Id = 456 }).Subscribe(observer);

            // wait some time until we think all messages have been sent and received over AMQ
            Thread.Sleep(TimeSpan.FromSeconds(1));

            // test we got everything we expected
            Assert.That(messages.OfType<TestCommand>().Count() == 1, "TestCommand not received");
            Assert.That(messages.OfType<TestCommand2>().Count() == 1, "TestCommand2 not received");
            Assert.That(messages.OfType<TestCommand3>().Count() == 1, "TestCommand3 not received");
            Assert.That(messages.OfType<TestEvent>().Count() == 1, "TestEvent not received");
            Assert.That(messages.OfType<TestRequest>().Count() == 1, "TestRequest not received");
            Assert.That(messages.OfType<TestResponse>().Count() == 1, "TestResponse not received");

            subscriptions.Dispose();
            ((IDisposable)serviceBus).Dispose();
            // win!
        }

        [Test]
        public void TestServiceBusWithEmbeddedBrokerAndSharedConnection()
        {
            // use the embedded broker
            var brokerUri = _broker.FailoverUri;

            // property filters and providers
            Func<IDictionary, bool> propertyFilter = properties =>
            {
                Console.WriteLine("Filtering message by properties");
                return properties.Count > 0 && (int?)properties["Id"] == 123;
            };

            Func<IMessage, Dictionary<string, object>> propertyProvider = message =>
            {
                Console.WriteLine("Providing message properties");
                var testMessage1 = message as ITestMessage1;
                return testMessage1 != null
                    ? new Dictionary<string, object> {{"Id", testMessage1.Id}, {"HostName", Dns.GetHostName()}}
                    : null;
            };

            // set up ServiceBus using fluent interfaces and all current endpoints and pointing at test AMQ broker
            IServiceBus serviceBus = ServiceBus.Configure()
                .WithActiveMQSharedConnectionScope(brokerUri, config => config
                    .WithActiveMQEndpoints<ITestMessage1>()
                        .Named("Obvs.TestService1")
                        .UsingQueueFor<TestCommand>().ClientAcknowledge()
                        .UsingQueueFor<TestCommand2>().ClientAcknowledge()
                        .UsingQueueFor<IRequest>().AutoAcknowledge()
                        .FilterReceivedMessages(propertyFilter)
                        .AppendMessageProperties(propertyProvider)
                        .SerializedAsJson()
                        .AsClientAndServer()
                    .WithActiveMQEndpoints<ITestMessage2>()
                        .Named("Obvs.TestService2")
                        .SerializedAsJson()
                        .AsClientAndServer())
                .UsingConsoleLogging()
                .Create();

            // create threadsafe collection to hold received messages in
            ConcurrentBag<IMessage> messages = new ConcurrentBag<IMessage>();

            // create some actions that will act as a fake services acting on incoming commands and requests
            Action<TestCommand> fakeService1 = command => serviceBus.PublishAsync(new TestEvent {Id = command.Id});
            Action<Test2Command> fakeService3 = command => serviceBus.PublishAsync(new Test2Event {Id = command.Id});
            Action<TestRequest> fakeService2 = request => serviceBus.ReplyAsync(request, new TestResponse {Id = request.Id});
            AnonymousObserver<IMessage> observer = new AnonymousObserver<IMessage>(messages.Add, Console.WriteLine, () => Console.WriteLine("OnCompleted"));

            // subscribe to all messages on the ServiceBus
            serviceBus.Events.Subscribe(observer);
            serviceBus.Commands.Subscribe(observer);
            serviceBus.Requests.Subscribe(observer);
            serviceBus.Commands.OfType<TestCommand>().Subscribe(fakeService1);
            serviceBus.Commands.OfType<Test2Command>().Subscribe(fakeService3);
            serviceBus.Requests.OfType<TestRequest>().Subscribe(fakeService2);

            // send some messages
            serviceBus.SendAsync(new TestCommand { Id = 123 });
            serviceBus.SendAsync(new Test2Command { Id = 123 });
            serviceBus.SendAsync(new TestCommand2 { Id = 123 });
            serviceBus.SendAsync(new TestCommand3 { Id = 123 });
            serviceBus.GetResponses(new TestRequest { Id = 456 }).Subscribe(observer);

            // wait some time until we think all messages have been sent and received over AMQ
            Thread.Sleep(TimeSpan.FromSeconds(1));

            // test we got everything we expected
            Assert.That(messages.OfType<TestCommand>().Count() == 1, "TestCommand not received");
            Assert.That(messages.OfType<Test2Command>().Count() == 1, "Test2Command not received");
            Assert.That(messages.OfType<TestCommand2>().Count() == 1, "TestCommand2 not received");
            Assert.That(messages.OfType<TestCommand3>().Count() == 1, "TestCommand3 not received");
            Assert.That(messages.OfType<TestEvent>().Count() == 1, "TestEvent not received");
            Assert.That(messages.OfType<Test2Event>().Count() == 1, "Test2Event not received");
            Assert.That(messages.OfType<TestRequest>().Count() == 1, "TestRequest not received");
            Assert.That(messages.OfType<TestResponse>().Count() == 1, "TestResponse not received");

            ((IDisposable)serviceBus).Dispose();
            // win!
        }
        
        [Test]
        public void TestServiceBusWithEmbeddedBrokerAndSharedConnectionAndLocalBus()
        {
            // use the embedded broker
            var brokerUri = _broker.FailoverUri;

            // set up ServiceBus using fluent interfaces and all current endpoints and pointing at test AMQ broker
            IServiceBus serviceBus = ServiceBus.Configure()
                .WithActiveMQSharedConnectionScope(brokerUri, config => config 
                    .WithActiveMQEndpoints<ITestMessage1>()
                        .Named("Obvs.TestService1")
                        .UsingQueueFor<TestCommand>().ClientAcknowledge()
                        .UsingQueueFor<TestCommand2>().ClientAcknowledge()
                        .UsingQueueFor<IRequest>().AutoAcknowledge()
                        .SerializedAsJson()
                        .AsServer()
                    .WithActiveMQEndpoints<ITestMessage2>()
                        .Named("Obvs.TestService2")
                        .SerializedAsJson()
                        .AsServer())
                .PublishLocally().AnyMessagesWithNoEndpointClients()
                .UsingConsoleLogging()
                .Create();

            // create threadsafe collection to hold received messages in
            ConcurrentBag<IMessage> messages = new ConcurrentBag<IMessage>();

            // create some actions that will act as a fake services acting on incoming commands and requests
            Action<TestCommand> fakeService1 = command => serviceBus.PublishAsync(new TestEvent {Id = command.Id});
            Action<Test2Command> fakeService3 = command => serviceBus.PublishAsync(new Test2Event {Id = command.Id});
            Action<TestRequest> fakeService2 = request => serviceBus.ReplyAsync(request, new TestResponse {Id = request.Id});
            AnonymousObserver<IMessage> observer = new AnonymousObserver<IMessage>(messages.Add, Console.WriteLine, () => Console.WriteLine("OnCompleted"));

            // subscribe to all messages on the ServiceBus
            serviceBus.Events.Subscribe(observer);
            serviceBus.Commands.Subscribe(observer);
            serviceBus.Requests.Subscribe(observer);
            serviceBus.Commands.OfType<TestCommand>().Subscribe(fakeService1);
            serviceBus.Commands.OfType<Test2Command>().Subscribe(fakeService3);
            serviceBus.Requests.OfType<TestRequest>().Subscribe(fakeService2);

            // send some messages
            serviceBus.SendAsync(new TestCommand { Id = 123 });
            serviceBus.SendAsync(new Test2Command { Id = 123 });
            serviceBus.SendAsync(new TestCommand2 { Id = 123 });
            serviceBus.SendAsync(new TestCommand3 { Id = 123 });
            serviceBus.GetResponses(new TestRequest { Id = 456 }).Subscribe(observer);

            // wait some time until we think all messages have been sent and received over AMQ
            Thread.Sleep(TimeSpan.FromSeconds(1));

            // test we got everything we expected
            Assert.That(messages.OfType<TestCommand>().Count() == 1, "TestCommand not received");
            Assert.That(messages.OfType<Test2Command>().Count() == 1, "Test2Command not received");
            Assert.That(messages.OfType<TestCommand2>().Count() == 1, "TestCommand2 not received");
            Assert.That(messages.OfType<TestCommand3>().Count() == 1, "TestCommand3 not received");
            Assert.That(messages.OfType<TestEvent>().Count() == 1, "TestEvent not received");
            Assert.That(messages.OfType<Test2Event>().Count() == 1, "Test2Event not received");
            Assert.That(messages.OfType<TestRequest>().Count() == 1, "TestRequest not received");
            Assert.That(messages.OfType<TestResponse>().Count() == 1, "TestResponse not received");

            ((IDisposable)serviceBus).Dispose();
            // win!
        }


        public interface ITestMessage1 : IMessage
        {
            int Id { get; }
        }

        public interface ITestMessage2 : IMessage
        {
        }

        public class TestEvent : ITestMessage1, IEvent
        {
            public int Id { get; set; }

            public override string ToString()
            {
                return string.Format("{0}[Id={1}]", GetType().Name, Id);
            }
        }

        public class TestCommand : ITestMessage1, ICommand
        {
            public int Id { get; set; }

            public override string ToString()
            {
                return string.Format("{0}[Id={1}]", GetType().Name, Id);
            }
        }

        public class Test2Event : ITestMessage1, IEvent
        {
            public int Id { get; set; }

            public override string ToString()
            {
                return string.Format("{0}[Id={1}]", GetType().Name, Id);
            }
        }

        public class Test2Command : ITestMessage1, ICommand
        {
            public int Id { get; set; }

            public override string ToString()
            {
                return string.Format("{0}[Id={1}]", GetType().Name, Id);
            }
        }

        public class TestCommand2 : ITestMessage1, ICommand
        {
            public int Id { get; set; }

            public override string ToString()
            {
                return string.Format("{0}[Id={1}]", GetType().Name, Id);
            }
        }

        public class TestCommand3 : ITestMessage1, ICommand
        {
            public int Id { get; set; }

            public override string ToString()
            {
                return string.Format("{0}[Id={1}]", GetType().Name, Id);
            }
        }

        public class TestRequest : ITestMessage1, IRequest
        {
            public int Id { get; set; }

            public override string ToString()
            {
                return string.Format("{0}[Id={1}]", GetType().Name, Id);
            }

            public string RequestId { get; set; }
            public string RequesterId { get; set; }
        }

        public class TestResponse : ITestMessage1, IResponse
        {
            public int Id { get; set; }

            public override string ToString()
            {
                return string.Format("{0}[Id={1}]", GetType().Name, Id);
            }

            public string RequestId { get; set; }
            public string RequesterId { get; set; }
        }
    }
}