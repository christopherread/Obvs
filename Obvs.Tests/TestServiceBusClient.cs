using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using Obvs.Types;
using Xunit;

namespace Obvs.Tests
{
    public class TestServiceBusClient
    {
        [Fact]
        public async Task Given_NoEndpoints_When_SendAsyncMultiple_NoCommands_Assert_DoesNothing()
        {
            var serviceEndpointClients = Enumerable.Empty<IServiceEndpointClient<IMessage, ICommand, IEvent, IRequest, IResponse>>();
            var serviceEndpoints = Enumerable.Empty<IServiceEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse>>();
            var sut = new ServiceBusClient<IMessage, ICommand, IEvent, IRequest, IResponse>(serviceEndpointClients, serviceEndpoints, new DefaultRequestCorrelationProvider());

            var commands = new ICommand[0];

            await sut.SendAsync(commands);
        }

        [Fact]
        public async Task Given_NoEndpoints_When_SendAsyncMultiple_SingleCommand_Assert_ThrowsNoEndpointsConfigured()
        {
            var serviceEndpointClients = Enumerable.Empty<IServiceEndpointClient<IMessage, ICommand, IEvent, IRequest, IResponse>>();
            var serviceEndpoints = Enumerable.Empty<IServiceEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse>>();
            var sut = new ServiceBusClient<IMessage, ICommand, IEvent, IRequest, IResponse>(serviceEndpointClients, serviceEndpoints, new DefaultRequestCorrelationProvider());

            var commands = new[]{new TestServiceCommand1()};
            
            await Assert.ThrowsAsync<AggregateException>(() => sut.SendAsync(commands));
        }

        [Fact]
        public async Task Given_NoEndpointsForCommand_When_SendAsyncMultiple_SingleCommand_Assert_ThrowsNoEndpointsConfigured()
        {
            var serviceEndpointClients = new IServiceEndpointClient<IMessage, ICommand, IEvent, IRequest, IResponse>[]{new FakeServiceEndpoint(typeof(TestServiceCommand2))};
            var serviceEndpoints = Enumerable.Empty<IServiceEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse>>();
            var sut = new ServiceBusClient<IMessage, ICommand, IEvent, IRequest, IResponse>(serviceEndpointClients, serviceEndpoints, new DefaultRequestCorrelationProvider());

            var commands = new[] { new TestServiceCommand1() };

            await Assert.ThrowsAsync<AggregateException>(() => sut.SendAsync(commands));
        }

        [Fact]
        public async Task Given_MultipleEndpointsForDifferentServices_When_SendAsyncMultiple_SingleCommand_Assert_CallsSendOnCorrectEndpointClient()
        {
            var signal = new Subject<Unit>();

            var wrongEndpoint = new FakeServiceEndpoint(typeof(TestServiceCommand2));
            var wrongCommands = wrongEndpoint.Messages.TakeUntil(signal).ToList().ToTask();
            var correctEndpoint = new FakeServiceEndpoint(typeof(TestServiceCommand1));
            var correctCommands = correctEndpoint.Messages.TakeUntil(signal).ToList().ToTask();

            var serviceEndpointClients = new [] { wrongEndpoint, correctEndpoint };
            var serviceEndpoints = Enumerable.Empty<IServiceEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse>>();
            var sut = new ServiceBusClient<IMessage, ICommand, IEvent, IRequest, IResponse>(serviceEndpointClients, serviceEndpoints, new DefaultRequestCorrelationProvider());

            var cmd = new TestServiceCommand1();
            var commands = new[] { cmd };

            await sut.SendAsync(commands);

            signal.OnNext(Unit.Default);

            Assert.Empty(await wrongCommands);
            var sentCommands = await correctCommands;
            Assert.NotEmpty(sentCommands);
            Assert.Contains(cmd, sentCommands);
        }

        [Fact]
        public async Task Given_MultipleEndpointsForSameServices_When_SendAsyncMultiple_SingleCommand_Assert_CallsSendOnBothEndpointClient()
        {
            var signal = new Subject<Unit>();

            var wrongEndpoint = new FakeServiceEndpoint(typeof(TestServiceCommand1));
            var wrongCommands = wrongEndpoint.Messages.TakeUntil(signal).ToList().ToTask();
            var correctEndpoint = new FakeServiceEndpoint(typeof(TestServiceCommand1));
            var correctCommands = correctEndpoint.Messages.TakeUntil(signal).ToList().ToTask();

            var serviceEndpointClients = new[] { wrongEndpoint, correctEndpoint };
            var serviceEndpoints = Enumerable.Empty<IServiceEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse>>();
            var sut = new ServiceBusClient<IMessage, ICommand, IEvent, IRequest, IResponse>(serviceEndpointClients, serviceEndpoints, new DefaultRequestCorrelationProvider());

            var cmd = new TestServiceCommand1();
            var commands = new[] { cmd };

            await sut.SendAsync(commands);

            signal.OnNext(Unit.Default);

            var sentCommands1 = await wrongCommands;
            Assert.NotEmpty(sentCommands1);
            Assert.Contains(cmd, sentCommands1);
            var sendCommands2 = await correctCommands;
            Assert.NotEmpty(sendCommands2);
            Assert.Contains(cmd, sendCommands2);
        }
    }
}
