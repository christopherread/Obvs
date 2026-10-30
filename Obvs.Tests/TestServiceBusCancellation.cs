using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FakeItEasy;
using Obvs.Transports;
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

            A.CallTo(() => endpoint.PublishAsync(A<IEvent>._, A<IReadOnlyDictionary<string, string>>._, A<CancellationToken>._)).MustNotHaveHappened();
        }

        [Fact]
        public async Task ShouldStopPublishingToFurtherEndpointsWhenCancelled()
        {
            var ev = A.Fake<IEvent>();
            var first = EndpointFor(ev);
            var second = EndpointFor(ev);
            using var cancellation = new CancellationTokenSource();
            A.CallTo(() => first.PublishAsync(ev, A<IReadOnlyDictionary<string, string>>._, A<CancellationToken>._)).Invokes(() => cancellation.Cancel()).Returns(Task.CompletedTask);
            var serviceBus = new ServiceBus(NoEndpointClients, new[] { first, second });

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => serviceBus.PublishAsync(ev, cancellation.Token));

            A.CallTo(() => first.PublishAsync(ev, A<IReadOnlyDictionary<string, string>>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
            A.CallTo(() => second.PublishAsync(A<IEvent>._, A<IReadOnlyDictionary<string, string>>._, A<CancellationToken>._)).MustNotHaveHappened();
        }

        [Fact]
        public async Task ShouldNotReplyWhenAlreadyCancelled()
        {
            var request = A.Fake<IRequest>();
            var response = A.Fake<IResponse>();
            var endpoint = EndpointFor(response);
            var serviceBus = new ServiceBus(NoEndpointClients, new[] { endpoint });

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => serviceBus.ReplyAsync(request, response, Cancelled()));

            A.CallTo(() => endpoint.ReplyAsync(A<IRequest>._, A<IResponse>._, A<IReadOnlyDictionary<string, string>>._, A<CancellationToken>._)).MustNotHaveHappened();
        }

        [Fact]
        public async Task ShouldStopReplyingToFurtherEndpointsWhenCancelled()
        {
            var request = A.Fake<IRequest>();
            var response = A.Fake<IResponse>();
            var first = EndpointFor(response);
            var second = EndpointFor(response);
            using var cancellation = new CancellationTokenSource();
            A.CallTo(() => first.ReplyAsync(request, response, A<IReadOnlyDictionary<string, string>>._, A<CancellationToken>._)).Invokes(() => cancellation.Cancel()).Returns(Task.CompletedTask);
            var serviceBus = new ServiceBus(NoEndpointClients, new[] { first, second });

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => serviceBus.ReplyAsync(request, response, cancellation.Token));

            A.CallTo(() => second.ReplyAsync(A<IRequest>._, A<IResponse>._, A<IReadOnlyDictionary<string, string>>._, A<CancellationToken>._)).MustNotHaveHappened();
        }

        [Fact]
        public async Task ShouldNotSendWhenAlreadyCancelled()
        {
            var command = A.Fake<ICommand>();
            var endpointClient = EndpointClientFor(command);
            var serviceBus = new ServiceBus(new[] { endpointClient }, NoEndpoints);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => serviceBus.SendAsync(command, Cancelled()));

            A.CallTo(() => endpointClient.SendAsync(A<ICommand>._, A<IReadOnlyDictionary<string, string>>._, A<CancellationToken>._)).MustNotHaveHappened();
        }

        [Fact]
        public async Task ShouldStopSendingToFurtherEndpointClientsWhenCancelled()
        {
            var command = A.Fake<ICommand>();
            var first = EndpointClientFor(command);
            var second = EndpointClientFor(command);
            using var cancellation = new CancellationTokenSource();
            A.CallTo(() => first.SendAsync(command, A<IReadOnlyDictionary<string, string>>._, A<CancellationToken>._)).Invokes(() => cancellation.Cancel()).Returns(Task.CompletedTask);
            var serviceBus = new ServiceBus(new[] { first, second }, NoEndpoints);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => serviceBus.SendAsync(command, cancellation.Token));

            A.CallTo(() => second.SendAsync(A<ICommand>._, A<IReadOnlyDictionary<string, string>>._, A<CancellationToken>._)).MustNotHaveHappened();
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

            A.CallTo(() => endpointClient.SendAsync(A<ICommand>._, A<IReadOnlyDictionary<string, string>>._, A<CancellationToken>._)).MustNotHaveHappened();
        }

        [Fact]
        public async Task ShouldStopSendingFurtherCommandsWhenCancelled()
        {
            var first = A.Fake<ICommand>();
            var second = A.Fake<ICommand>();
            var endpointClient = A.Fake<IServiceEndpointClient>();
            A.CallTo(() => endpointClient.CanHandle(A<IMessage>._)).Returns(true);
            using var cancellation = new CancellationTokenSource();
            A.CallTo(() => endpointClient.SendAsync(first, A<IReadOnlyDictionary<string, string>>._, A<CancellationToken>._)).Invokes(() => cancellation.Cancel()).Returns(Task.CompletedTask);
            var serviceBus = new ServiceBus(new[] { endpointClient }, NoEndpoints);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => serviceBus.SendAsync(new[] { first, second }, cancellation.Token));

            A.CallTo(() => endpointClient.SendAsync(second, A<IReadOnlyDictionary<string, string>>._, A<CancellationToken>._)).MustNotHaveHappened();
        }

        [Fact]
        public async Task ShouldReportCancellationRatherThanTheErrorsItCaused()
        {
            // The endpoint client is cancelled while sending and fails with it, so the caller sees the cancellation,
            // not an AggregateException of it
            var command = A.Fake<ICommand>();
            var endpointClient = EndpointClientFor(command);
            using var cancellation = new CancellationTokenSource();
            A.CallTo(() => endpointClient.SendAsync(command, A<IReadOnlyDictionary<string, string>>._, A<CancellationToken>._)).Invokes(() => cancellation.Cancel()).Throws(() => new OperationCanceledException(cancellation.Token));
            var serviceBus = new ServiceBus(new[] { endpointClient }, NoEndpoints);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => serviceBus.SendAsync(command, cancellation.Token));
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

            A.CallTo(() => endpoint.PublishAsync(ev, A<IReadOnlyDictionary<string, string>>._, token)).MustHaveHappenedOnceExactly();
            A.CallTo(() => endpointClient.SendAsync(command, A<IReadOnlyDictionary<string, string>>._, token)).MustHaveHappenedOnceExactly();
            A.CallTo(() => endpoint.ReplyAsync(request, response, A<IReadOnlyDictionary<string, string>>._, token)).MustHaveHappenedOnceExactly();
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

            await endpoint.PublishAsync(ev, new Dictionary<string, string>(), token);
            await endpoint.ReplyAsync(A.Fake<IRequest>(), response, new Dictionary<string, string>(), token);
            await endpointClient.SendAsync(command, new Dictionary<string, string>(), token);

            A.CallTo(() => eventPublisher.PublishAsync(ev, A<IReadOnlyDictionary<string, string>>._, token)).MustHaveHappenedOnceExactly();
            A.CallTo(() => responsePublisher.PublishAsync(response, A<IReadOnlyDictionary<string, string>>._, token)).MustHaveHappenedOnceExactly();
            A.CallTo(() => commandPublisher.PublishAsync(command, A<IReadOnlyDictionary<string, string>>._, token)).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public async Task TypeRoutingPublisherShouldPassTheTokenOn()
        {
            var ev = A.Fake<IEvent>();
            var eventPublisher = A.Fake<IMessagePublisher<IMessage>>();
            var publisher = new TypeRoutingMessagePublisher<IMessage>(new[] { new KeyValuePair<Type, IMessagePublisher<IMessage>>(typeof(IEvent), eventPublisher) });
            using var cancellation = new CancellationTokenSource();
            var token = cancellation.Token;

            await publisher.PublishAsync(ev, new Dictionary<string, string>(), token);

            A.CallTo(() => eventPublisher.PublishAsync(ev, A<IReadOnlyDictionary<string, string>>._, token)).MustHaveHappenedOnceExactly();
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
