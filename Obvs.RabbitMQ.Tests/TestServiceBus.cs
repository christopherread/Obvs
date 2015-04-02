using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using NUnit.Framework;
using Obvs.RabbitMQ.Configuration;
using Obvs.RabbitMQ.Tests.Messages;
using Obvs.Serialization.Json.Configuration;
using Obvs.Types;

namespace Obvs.RabbitMQ.Tests
{
    [TestFixture]
    public class TestServiceBus
    {
        [Test, Explicit]
        public void ShouldSendAndReceiveMessagesOverServiceBus()
        {
            IServiceBus serviceBus = ServiceBus.Configure()
                    .WithRabbitMQEndpoints<ITestMessage>()
                        .Named("Obvs.TestService")
                        .ConnectToBroker("amqp://localhost")
                        .SerializedAsJson()
                        .AsClientAndServer()
                    .Create();

            // create threadsafe collection to hold received messages in
            ConcurrentBag<IMessage> messages = new ConcurrentBag<IMessage>();

            // create some actions that will act as a fake services acting on incoming commands and requests
            Action<TestCommand> fakeService1 = command => serviceBus.PublishAsync(new TestEvent { Id = command.Id });
            Action<TestRequest> fakeService2 = request => serviceBus.ReplyAsync(request, new TestResponse { Id = request.Id });
            AnonymousObserver<IMessage> observer = new AnonymousObserver<IMessage>(msg => { messages.Add(msg); Console.WriteLine(msg); }, exception => Console.WriteLine(exception));

            // subscribe to all messages on the ServiceBus
            serviceBus.Events.Subscribe(observer);
            serviceBus.Commands.Subscribe(observer);
            serviceBus.Requests.Subscribe(observer);
            serviceBus.Commands.OfType<TestCommand>().Subscribe(fakeService1);
            serviceBus.Requests.OfType<TestRequest>().Subscribe(fakeService2);

            // send some messages
            serviceBus.SendAsync(new TestCommand { Id = 123 });
            serviceBus.GetResponses(new TestRequest { Id = 456 }).Subscribe(observer);

            // wait some time until we think all messages have been sent and received from RabbitMQ
            Thread.Sleep(TimeSpan.FromSeconds(1));

            // test we got everything we expected
            Assert.That(messages.OfType<TestCommand>().Count() == 1, "TestCommand not received");
            Assert.That(messages.OfType<TestEvent>().Count() == 1, "TestEvent not received");
            Assert.That(messages.OfType<TestRequest>().Count() == 1, "TestRequest not received");
            Assert.That(messages.OfType<TestResponse>().Count() == 1, "TestResponse not received");
        }
    }

    
}