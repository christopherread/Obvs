using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using kafka4net;
using kafka4net.ConsumerImpl;
using NLog;
using NLog.Config;
using NLog.Targets;
using NUnit.Framework;
using Obvs.Configuration;
using Obvs.Kafka.Configuration;
using Obvs.Serialization.Json.Configuration;
using Obvs.Types;

namespace Obvs.Kafka.Tests
{
    [TestFixture]
    public class Kafka4NetTests
    {
        string _seed2Addresses = "192.168.99.101";
         Random _rnd = new Random();

        [SetUp]
        public void SetUp()
        {
            var config = new LoggingConfiguration();

            // Step 2. Create targets and add them to the configuration 
            var consoleTarget = new ColoredConsoleTarget();
            config.AddTarget("console", consoleTarget);

            var fileTarget = new FileTarget();
            config.AddTarget("file", fileTarget);

            // Step 3. Set target properties 
            consoleTarget.Layout = @"${date:format=HH\:mm\:ss} ${logger} ${message}";
            fileTarget.FileName = "${basedir}/file.txt";
            fileTarget.Layout = "${message}";

            // Step 4. Define rules
            var rule1 = new LoggingRule("*", LogLevel.Trace, consoleTarget);
            config.LoggingRules.Add(rule1);

            // Step 5. Activate the configuration
            NLog.LogManager.Configuration = config;

            kafka4net.Logger.SetupNLog();
        }

        [Test]
        [Explicit]
        public async Task TestServiceBusWithRemoteKafka1()
        {
            var topic = "autocreate.test." + _rnd.Next();
            const int producedCount = 505;
            var lala = Encoding.UTF8.GetBytes("la-la-la");
            // TODO: set wait to 5sec

            //
            // Produce
            // In order to make sure that topic was created by producer, send and wait for producer
            // completion before performing validation read.
            //
            var producer = new Producer(_seed2Addresses, new ProducerConfiguration(topic));

            await producer.ConnectAsync();

            Console.WriteLine("Producing...");
            await Observable.Repeat(true).
                Take(producedCount).
                Do(_ => producer.Send(new Message { Value = lala })).
                ToTask();
            await producer.CloseAsync(TimeSpan.FromSeconds(10));

            //
            // Validate by reading published messages
            //
            var consumer = new Consumer(new ConsumerConfiguration(_seed2Addresses, topic, new StartPositionTopicStart(), maxWaitTimeMs: 1000, minBytesPerFetch: 1));
            var msgs = consumer.OnMessageArrived.Publish().RefCount();
            var receivedTxt = new List<string>();
            var consumerSubscription = msgs.
                Select(m => Encoding.UTF8.GetString(m.Value)).
                Synchronize(). // protect receivedTxt
                Do(m => Console.WriteLine("Received {0}", m)).
                Do(receivedTxt.Add).
                Subscribe();
            await consumer.IsConnected;

            Console.WriteLine("Waiting for consumer");
            await msgs.Take(producedCount).TakeUntil(DateTimeOffset.Now.AddSeconds(5)).LastOrDefaultAsync().ToTask();

            Assert.AreEqual(producedCount, receivedTxt.Count, "Did not received all messages");
            Assert.IsTrue(receivedTxt.All(m => m == "la-la-la"), "Unexpected message content");

            consumerSubscription.Dispose();
            consumer.Dispose();
        }

        [Test]
        [Explicit]
        public void TestServiceBusWithRemoteKafka()
        {
            
            IServiceBus serviceBus = ServiceBus.Configure()
                .WithKafkaEndpoints<ITestMessage1>()
                    .Named("Obvs.TestService")
                    .ConnectToKafka(_seed2Addresses)
                    .SerializedAsJson()
                    .AsClientAndServer()
                .PublishLocally()
                    .OnlyMessagesWithNoEndpoints()
                .UsingConsoleLogging()
                .Create();

            ConcurrentBag<IMessage> messages = new ConcurrentBag<IMessage>();

            // create some actions that will act as a fake services acting on incoming commands and requests
            Action<TestCommand> fakeService1 = command => serviceBus.PublishAsync(new TestEvent { Id = command.Id });
            Action<TestRequest> fakeService2 = request => serviceBus.ReplyAsync(request, new TestResponse { Id = request.Id });
            AnonymousObserver<IMessage> observer = new AnonymousObserver<IMessage>(x =>
            {
                Console.WriteLine("********* " + x);
                messages.Add(x);
            }, Console.WriteLine, () => Console.WriteLine("OnCompleted"));

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
            Thread.Sleep(TimeSpan.FromSeconds(10));

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


        public interface ITestMessage1 : IMessage
        {
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
