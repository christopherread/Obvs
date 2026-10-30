using System;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;
using Obvs.Configuration;
using Obvs.Hosting;
using Obvs.Testing;
using Obvs.Tests.Hosting.Handlers;
using Obvs.Transports;
using Obvs.Types;
using Xunit;

namespace Obvs.Tests.Hosting
{
    // A bus for scripts and console tools, with the same configuration and lifetime as AddObvs but no host
    public class TestObvsBus
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        [Fact]
        public async Task ShouldSendAndReceiveWithoutAHost()
        {
            await using var bus = await ObvsBus.StartAsync(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service.UseInMemory()), Ct);

            var received = bus.Commands.OfType<PlaceOrder>().FirstAsync().ToTask();
            await bus.SendAsync(new PlaceOrder("o-1"), Ct);

            Assert.Equal("o-1", (await received.WaitAsync(Timeout, Ct)).OrderId);
        }

        [Fact]
        public async Task ShouldRunHandlers()
        {
            var placed = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            await using var bus = await ObvsBus.StartAsync(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service
                    .UseInMemory()
                    .HandleCommand<PlaceOrder>((command, context, ct) => context.PublishAsync(new OrderPlaced(command.OrderId), ct))
                    .HandleEvent<OrderPlaced>((ev, context, ct) =>
                    {
                        placed.TrySetResult(ev.OrderId);
                        return Task.CompletedTask;
                    })), Ct);

            await bus.SendAsync(new PlaceOrder("o-1"), Ct);

            Assert.Equal("o-1", await placed.Task.WaitAsync(Timeout, Ct));
        }

        [Fact]
        public async Task ShouldResolveClassHandlersFromTheServicesGiven()
        {
            var recorder = new Recorder();
            await using var bus = await ObvsBus.StartAsync(
                services => services.AddSingleton(recorder),
                obvs => obvs.AddService<IOrderMessage>("orders", service => service
                    .UseInMemory()
                    .HandleCommand<PlaceOrder, RecordingHandler>()), Ct);

            await bus.SendAsync(new PlaceOrder("o-1"), Ct);

            Assert.Equal("o-1", await recorder.OrderId.Task.WaitAsync(Timeout, Ct));
            Assert.Same(recorder, bus.Services.GetRequiredService<Recorder>());
        }

        [Fact]
        public async Task ShouldFailToStartWithTheConfigurationErrors()
        {
            var exception = await Assert.ThrowsAsync<ObvsConfigurationException>(() => ObvsBus.StartAsync(obvs => obvs
                .AddService<IOrderMessage>("orders", service => { }), Ct));

            Assert.Contains("has no transport", Assert.Single(exception.Errors));
        }

        [Fact]
        public async Task ShouldDisposeTheEndpointsWhenDisposed()
        {
            var endpoint = A.Fake<IServiceEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse>>();
            var endpointClient = A.Fake<IServiceEndpointClient<IMessage, ICommand, IEvent, IRequest, IResponse>>();
            var provider = A.Fake<IServiceEndpointProvider<IMessage, ICommand, IEvent, IRequest, IResponse>>();
            A.CallTo(() => provider.CreateEndpoint()).Returns(endpoint);
            A.CallTo(() => provider.CreateEndpointClient()).Returns(endpointClient);
            var bus = await ObvsBus.StartAsync(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service.UseEndpointProvider(_ => provider)), Ct);

            await bus.DisposeAsync();

            A.CallTo(() => endpoint.Dispose()).MustHaveHappened();
            A.CallTo(() => endpointClient.Dispose()).MustHaveHappened();
        }

        public sealed class Recorder
        {
            public TaskCompletionSource<string> OrderId { get; } = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public sealed class RecordingHandler : ICommandHandler<PlaceOrder>
        {
            private readonly Recorder _recorder;

            public RecordingHandler(Recorder recorder) => _recorder = recorder;

            public Task HandleAsync(PlaceOrder command, MessageContext context, CancellationToken cancellationToken)
            {
                _recorder.OrderId.TrySetResult(command.OrderId);
                return Task.CompletedTask;
            }
        }
    }
}
