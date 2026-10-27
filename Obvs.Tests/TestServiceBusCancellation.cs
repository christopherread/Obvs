using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FakeItEasy;
using Obvs.Configuration;
using Obvs.Types;
using Xunit;

namespace Obvs.Tests
{
    public class TestServiceBusCancellation
    {
        private static readonly IServiceEndpointClient[] NoEndpointClients = new IServiceEndpointClient[0];
        private static readonly IServiceEndpoint[] NoEndpoints = new IServiceEndpoint[0];

        [Fact]
        public async Task ShouldNotPublishWhenAlreadyCancelled()
        {
            var ev = A.Fake<IEvent>();
            var endpoint = EndpointFor(ev);
            var serviceBus = new ServiceBus(NoEndpointClients, new[] { endpoint });

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => serviceBus.PublishAsync(ev, Cancelled()));

            A.CallTo(() => endpoint.PublishAsync(A<IEvent>._, A<CancellationToken>._)).MustNotHaveHappened();
        }

        [Fact]
        public async Task ShouldStopPublishingToFurtherEndpointsWhenCancelled()
        {
            var ev = A.Fake<IEvent>();
            var first = EndpointFor(ev);
            var second = EndpointFor(ev);
            using var cancellation = new CancellationTokenSource();
            A.CallTo(() => first.PublishAsync(ev, A<CancellationToken>._)).Invokes(() => cancellation.Cancel()).Returns(Task.CompletedTask);
            var serviceBus = new ServiceBus(NoEndpointClients, new[] { first, second });

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => serviceBus.PublishAsync(ev, cancellation.Token));

            A.CallTo(() => first.PublishAsync(ev, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
            A.CallTo(() => second.PublishAsync(A<IEvent>._, A<CancellationToken>._)).MustNotHaveHappened();
        }

        [Fact]
        public async Task ShouldNotReplyWhenAlreadyCancelled()
        {
            var request = A.Fake<IRequest>();
            var response = A.Fake<IResponse>();
            var endpoint = EndpointFor(response);
            var serviceBus = new ServiceBus(NoEndpointClients, new[] { endpoint });

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => serviceBus.ReplyAsync(request, response, Cancelled()));

            A.CallTo(() => endpoint.ReplyAsync(A<IRequest>._, A<IResponse>._, A<CancellationToken>._)).MustNotHaveHappened();
        }

        [Fact]
        public async Task ShouldStopReplyingToFurtherEndpointsWhenCancelled()
        {
            var request = A.Fake<IRequest>();
            var response = A.Fake<IResponse>();
            var first = EndpointFor(response);
            var second = EndpointFor(response);
            using var cancellation = new CancellationTokenSource();
            A.CallTo(() => first.ReplyAsync(request, response, A<CancellationToken>._)).Invokes(() => cancellation.Cancel()).Returns(Task.CompletedTask);
            var serviceBus = new ServiceBus(NoEndpointClients, new[] { first, second });

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => serviceBus.ReplyAsync(request, response, cancellation.Token));

            A.CallTo(() => second.ReplyAsync(A<IRequest>._, A<IResponse>._, A<CancellationToken>._)).MustNotHaveHappened();
        }

        [Fact]
        public async Task ShouldNotSendWhenAlreadyCancelled()
        {
            var command = A.Fake<ICommand>();
            var endpointClient = EndpointClientFor(command);
            var serviceBus = new ServiceBus(new[] { endpointClient }, NoEndpoints);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => serviceBus.SendAsync(command, Cancelled()));

            A.CallTo(() => endpointClient.SendAsync(A<ICommand>._, A<CancellationToken>._)).MustNotHaveHappened();
        }

        [Fact]
        public async Task ShouldStopSendingToFurtherEndpointClientsWhenCancelled()
        {
            var command = A.Fake<ICommand>();
            var first = EndpointClientFor(command);
            var second = EndpointClientFor(command);
            using var cancellation = new CancellationTokenSource();
            A.CallTo(() => first.SendAsync(command, A<CancellationToken>._)).Invokes(() => cancellation.Cancel()).Returns(Task.CompletedTask);
            var serviceBus = new ServiceBus(new[] { first, second }, NoEndpoints);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => serviceBus.SendAsync(command, cancellation.Token));

            A.CallTo(() => second.SendAsync(A<ICommand>._, A<CancellationToken>._)).MustNotHaveHappened();
        }

        [Fact]
        public async Task ShouldNotSendAnyCommandsWhenAlreadyCancelled()
        {
            var commands = new[] { A.Fake<ICommand>(), A.Fake<ICommand>() };
            var endpointClient = A.Fake<IServiceEndpointClient>();
            A.CallTo(() => endpointClient.CanHandle(A<IMessage>._)).Returns(true);
            var serviceBus = new ServiceBus(new[] { endpointClient }, NoEndpoints);

            // Cancellation, not an AggregateException of each command's cancellation
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => serviceBus.SendAsync(commands, Cancelled()));

            A.CallTo(() => endpointClient.SendAsync(A<ICommand>._, A<CancellationToken>._)).MustNotHaveHappened();
        }

        [Fact]
        public async Task ShouldStopSendingFurtherCommandsWhenCancelled()
        {
            var first = A.Fake<ICommand>();
            var second = A.Fake<ICommand>();
            var endpointClient = A.Fake<IServiceEndpointClient>();
            A.CallTo(() => endpointClient.CanHandle(A<IMessage>._)).Returns(true);
            using var cancellation = new CancellationTokenSource();
            A.CallTo(() => endpointClient.SendAsync(first, A<CancellationToken>._)).Invokes(() => cancellation.Cancel()).Returns(Task.CompletedTask);
            var serviceBus = new ServiceBus(new[] { endpointClient }, NoEndpoints);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => serviceBus.SendAsync(new[] { first, second }, cancellation.Token));

            A.CallTo(() => endpointClient.SendAsync(second, A<CancellationToken>._)).MustNotHaveHappened();
        }

        [Fact]
        public async Task ShouldReportCancellationRatherThanTheErrorsItCaused()
        {
            // The local bus is cancelled while sending and fails with it, so the caller sees the cancellation,
            // not an AggregateException of it
            var command = A.Fake<ICommand>();
            var localBus = A.Fake<IServiceBus<IMessage, ICommand, IEvent, IRequest, IResponse>>();
            using var cancellation = new CancellationTokenSource();
            A.CallTo(() => localBus.SendAsync(command, A<CancellationToken>._)).Invokes(() => cancellation.Cancel()).Throws(() => new OperationCanceledException(cancellation.Token));
            var serviceBus = new ServiceBus<IMessage, ICommand, IEvent, IRequest, IResponse>(NoEndpointClients, NoEndpoints,
                new DefaultRequestCorrelationProvider(), localBus, LocalBusOptions.AllMessages);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => serviceBus.SendAsync(command, cancellation.Token));
        }

        [Fact]
        public async Task ShouldPassTheTokenToTheLocalBus()
        {
            var ev = A.Fake<IEvent>();
            var command = A.Fake<ICommand>();
            var request = A.Fake<IRequest>();
            var response = A.Fake<IResponse>();
            var localBus = A.Fake<IServiceBus<IMessage, ICommand, IEvent, IRequest, IResponse>>();
            var serviceBus = new ServiceBus<IMessage, ICommand, IEvent, IRequest, IResponse>(NoEndpointClients, NoEndpoints,
                new DefaultRequestCorrelationProvider(), localBus, LocalBusOptions.AllMessages);
            using var cancellation = new CancellationTokenSource();
            var token = cancellation.Token;

            await serviceBus.PublishAsync(ev, token);
            await serviceBus.SendAsync(command, token);
            await serviceBus.SendAsync(new[] { command }, token);
            await serviceBus.ReplyAsync(request, response, token);

            A.CallTo(() => localBus.PublishAsync(ev, token)).MustHaveHappenedOnceExactly();
            A.CallTo(() => localBus.SendAsync(command, token)).MustHaveHappenedTwiceExactly();
            A.CallTo(() => localBus.ReplyAsync(request, response, token)).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public async Task ShouldPassTheTokenThroughTheNonGenericBus()
        {
            var ev = A.Fake<IEvent>();
            var command = A.Fake<ICommand>();
            var commands = new[] { command };
            var request = A.Fake<IRequest>();
            var response = A.Fake<IResponse>();
            var inner = A.Fake<IServiceBus<IMessage, ICommand, IEvent, IRequest, IResponse>>();
            var serviceBus = new ServiceBus(inner);
            using var cancellation = new CancellationTokenSource();
            var token = cancellation.Token;

            await serviceBus.PublishAsync(ev, token);
            await serviceBus.SendAsync(command, token);
            await serviceBus.SendAsync(commands, token);
            await serviceBus.ReplyAsync(request, response, token);

            A.CallTo(() => inner.PublishAsync(ev, token)).MustHaveHappenedOnceExactly();
            A.CallTo(() => inner.SendAsync(command, token)).MustHaveHappenedOnceExactly();
            A.CallTo(() => inner.SendAsync(commands, token)).MustHaveHappenedOnceExactly();
            A.CallTo(() => inner.ReplyAsync(request, response, token)).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public async Task ShouldPassTheTokenThroughTheNonGenericClient()
        {
            var command = A.Fake<ICommand>();
            var commands = new[] { command };
            var inner = A.Fake<IServiceBusClient<IMessage, ICommand, IEvent, IRequest, IResponse>>();
            var client = new ServiceBusClient(inner);
            using var cancellation = new CancellationTokenSource();
            var token = cancellation.Token;

            await client.SendAsync(command, token);
            await client.SendAsync(commands, token);

            A.CallTo(() => inner.SendAsync(command, token)).MustHaveHappenedOnceExactly();
            A.CallTo(() => inner.SendAsync(commands, token)).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public async Task ShouldPassTheTokenToTheEndpoints()
        {
            var ev = A.Fake<IEvent>();
            var command = A.Fake<ICommand>();
            var request = A.Fake<IRequest>();
            var response = A.Fake<IResponse>();
            var endpoint = A.Fake<IServiceEndpoint>();
            A.CallTo(() => endpoint.CanHandle(A<IMessage>._)).Returns(true);
            var endpointClient = EndpointClientFor(command);
            var serviceBus = new ServiceBus(new[] { endpointClient }, new[] { endpoint });
            using var cancellation = new CancellationTokenSource();
            var token = cancellation.Token;

            await serviceBus.PublishAsync(ev, token);
            await serviceBus.SendAsync(command, token);
            await serviceBus.ReplyAsync(request, response, token);

            A.CallTo(() => endpoint.PublishAsync(ev, token)).MustHaveHappenedOnceExactly();
            A.CallTo(() => endpointClient.SendAsync(command, token)).MustHaveHappenedOnceExactly();
            A.CallTo(() => endpoint.ReplyAsync(request, response, token)).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public async Task EndpointsShouldPassTheTokenToTheirPublishers()
        {
            var ev = A.Fake<IEvent>();
            var command = A.Fake<ICommand>();
            var response = A.Fake<IResponse>();
            var eventPublisher = A.Fake<IMessagePublisher<IEvent>>();
            var responsePublisher = A.Fake<IMessagePublisher<IResponse>>();
            var commandPublisher = A.Fake<IMessagePublisher<ICommand>>();
            var endpoint = new ServiceEndpoint(A.Fake<IMessageSource<IRequest>>(), A.Fake<IMessageSource<ICommand>>(),
                eventPublisher, responsePublisher, typeof(IMessage));
            var endpointClient = new ServiceEndpointClient(A.Fake<IMessageSource<IEvent>>(), A.Fake<IMessageSource<IResponse>>(),
                A.Fake<IMessagePublisher<IRequest>>(), commandPublisher, typeof(IMessage));
            using var cancellation = new CancellationTokenSource();
            var token = cancellation.Token;

            await endpoint.PublishAsync(ev, token);
            await endpoint.ReplyAsync(A.Fake<IRequest>(), response, token);
            await endpointClient.SendAsync(command, token);

            A.CallTo(() => eventPublisher.PublishAsync(ev, token)).MustHaveHappenedOnceExactly();
            A.CallTo(() => responsePublisher.PublishAsync(response, token)).MustHaveHappenedOnceExactly();
            A.CallTo(() => commandPublisher.PublishAsync(command, token)).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public async Task TypeRoutingPublisherShouldPassTheTokenOn()
        {
            var ev = A.Fake<IEvent>();
            var eventPublisher = A.Fake<IMessagePublisher<IMessage>>();
            var publisher = new TypeRoutingMessagePublisher<IMessage>(new[] { new KeyValuePair<Type, IMessagePublisher<IMessage>>(typeof(IEvent), eventPublisher) });
            using var cancellation = new CancellationTokenSource();
            var token = cancellation.Token;

            await publisher.PublishAsync(ev, token);

            A.CallTo(() => eventPublisher.PublishAsync(ev, token)).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public async Task SubjectServiceBusShouldNotDeliverWhenAlreadyCancelled()
        {
            var serviceBus = new SubjectServiceBus(new DefaultRequestCorrelationProvider());
            var delivered = new List<IMessage>();
            serviceBus.Events.Subscribe(delivered.Add);
            serviceBus.Commands.Subscribe(delivered.Add);
            var request = new TestServiceRequest1();
            serviceBus.GetResponses(request).Subscribe(delivered.Add);
            delivered.Clear();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => serviceBus.PublishAsync(new TestServiceEvent1(), Cancelled()));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => serviceBus.SendAsync(new TestServiceCommand1(), Cancelled()));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => serviceBus.SendAsync(new ICommand[] { new TestServiceCommand1() }, Cancelled()));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => serviceBus.ReplyAsync(request, new TestServiceResponse1(), Cancelled()));

            Assert.Empty(delivered);
        }

        [Fact]
        public async Task SubjectServiceBusShouldStopSendingFurtherCommandsWhenCancelled()
        {
            var serviceBus = new SubjectServiceBus(new DefaultRequestCorrelationProvider());
            var first = new TestServiceCommand1();
            var second = new TestServiceCommand2();
            using var cancellation = new CancellationTokenSource();
            var delivered = new List<ICommand>();
            serviceBus.Commands.Subscribe(command =>
            {
                delivered.Add(command);
                cancellation.Cancel();
            });

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => serviceBus.SendAsync(new ICommand[] { first, second }, cancellation.Token));

            Assert.Equal(new ICommand[] { first }, delivered);
        }

        private static CancellationToken Cancelled() => new CancellationToken(true);

        private static IServiceEndpoint EndpointFor(IMessage message)
        {
            var endpoint = A.Fake<IServiceEndpoint>();
            A.CallTo(() => endpoint.CanHandle(message)).Returns(true);
            return endpoint;
        }

        private static IServiceEndpointClient EndpointClientFor(IMessage message)
        {
            var endpointClient = A.Fake<IServiceEndpointClient>();
            A.CallTo(() => endpointClient.CanHandle(message)).Returns(true);
            return endpointClient;
        }
    }
}
