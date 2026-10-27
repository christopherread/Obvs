using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Obvs.Configuration;
using Obvs.RabbitMQ.Configuration;
using Obvs.RabbitMQ.Tests.Messages;
using Obvs.Serialization.Json.Configuration;
using Obvs.Types;
using Xunit;

namespace Obvs.RabbitMQ.Tests
{
    public class TestServiceBus
    {
        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldNotDeadlockWhenASubscriberDisposesTheBus()
        {
            // Code that runs on receiving a message (including code after awaiting one) may shut the app down.
            // If messages arrived on RabbitMQ's consumer-dispatch thread, closing the connection there would
            // wait for that same thread, forever.
            IServiceBus serviceBus = ServiceBus.Configure()
                    .WithRabbitMQEndpoints<ITestMessage>()
                        .Named("Obvs.TestService.DisposeInCallback")
                        .ConnectToBroker(TestBroker.Uri)
                        .SerializedAsJson()
                        .AsClientAndServer()
                    .Create();
            var disposed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var subscription = serviceBus.Commands.OfType<TestCommand>().Subscribe(_ =>
            {
                ((IDisposable)serviceBus).Dispose();
                disposed.SetResult(true);
            });

            await serviceBus.SendAsync(new TestCommand { Id = 1 }, TestContext.Current.CancellationToken);

            Assert.True(await disposed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldSendAndReceiveMessagesOverServiceBus()
        {
            IServiceBus serviceBus = ServiceBus.Configure()
                    .WithRabbitMQEndpoints<ITestMessage>()
                        .Named("Obvs.TestService")
                        .ConnectToBroker(TestBroker.Uri)
                        .SerializedAsJson()
                        .AsClientAndServer()
                    .PublishLocally().AnyMessagesWithNoEndpointClients()
                    .UsingConsoleLogging()
                    .Create();

            // create threadsafe collection to hold received messages in
            var messages = new ConcurrentBag<IMessage>();

            // create some actions that will act as a fake services acting on incoming commands and requests
            Action<TestCommand> fakeService1 = command => serviceBus.PublishAsync(new TestEvent { Id = command.Id });
            Action<TestRequest> fakeService2 = request => serviceBus.ReplyAsync(request, new TestResponse { Id = request.Id });
            var observer = new AnonymousObserver<IMessage>(msg => { messages.Add(msg); Console.WriteLine(msg); }, exception => Console.WriteLine(exception));

            // subscribe to all messages on the ServiceBus
            var sub1 = serviceBus.Events.Subscribe(observer);
            var sub2 = serviceBus.Commands.Subscribe(observer);
            var sub3 = serviceBus.Requests.Subscribe(observer);
            var sub4 = serviceBus.Commands.OfType<TestCommand>().Subscribe(fakeService1);
            var sub5 = serviceBus.Requests.OfType<TestRequest>().Subscribe(fakeService2);

            // send some messages
            await serviceBus.SendAsync(new TestCommand { Id = 123 }, TestContext.Current.CancellationToken);
            var sub6 = serviceBus.GetResponses(new TestRequest { Id = 456 }).Subscribe(observer);

            // wait some time until we think all messages have been sent and received from RabbitMQ
            await Task.Delay(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

            // test we got everything we expected
            Assert.True(messages.OfType<TestCommand>().Count() == 1, "TestCommand not received");
            Assert.True(messages.OfType<TestEvent>().Count() == 1, "TestEvent not received");
            Assert.True(messages.OfType<TestRequest>().Count() == 1, "TestRequest not received");
            Assert.True(messages.OfType<TestResponse>().Count() == 1, "TestResponse not received");

            // dispose subscriptions
            sub1.Dispose();
            sub2.Dispose();
            sub3.Dispose();
            sub4.Dispose();
            sub5.Dispose();
            sub6.Dispose();

            // always call Dispose on serviceBus when exiting process
            ((IDisposable)serviceBus).Dispose();
        }
    }

    
}