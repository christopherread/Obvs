using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Obvs.Hosting;
using Obvs.Testing;
using Obvs.Types;
using Xunit;

namespace Obvs.Tests.Hosting.Handlers
{
    public class TestHandlerCancellation
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        [Fact]
        public async Task ShouldPassTheContextsTokenToTheBus()
        {
            var handlerToken = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
            var recording = new ConcurrentQueue<(string Operation, IMessage Message, CancellationToken Token)>();
            await using var harness = await StartOrdersAsync(recording, service => service
                .HandleCommand<PlaceOrder>(async (command, context, ct) =>
                {
                    handlerToken.TrySetResult(ct);
                    await context.PublishAsync(new OrderPlaced(command.OrderId), ct);
                    await context.SendAsync(new ShipOrder(command.OrderId), ct);
                }), shipping: true);

            await harness.Client.SendAsync(new PlaceOrder("o-1"), Ct);
            await harness.Published<OrderPlaced>(placed => placed.OrderId == "o-1");
            await harness.Sent<ShipOrder>(ship => ship.OrderId == "o-1");

            var token = await handlerToken.Task.WaitAsync(Timeout, Ct);
            Assert.True(token.CanBeCanceled);
            Assert.Equal(token, recording.Single(r => r.Message is OrderPlaced).Token);
            Assert.Equal(token, recording.Single(r => r.Message is ShipOrder).Token);
        }

        [Fact]
        public async Task ShouldReplyWithTheHandlersToken()
        {
            var handlerToken = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
            var recording = new ConcurrentQueue<(string Operation, IMessage Message, CancellationToken Token)>();
            await using var harness = await StartOrdersAsync(recording, service => service
                .HandleRequest<GetQuote, Quote>((request, context, ct) =>
                {
                    handlerToken.TrySetResult(ct);
                    return Task.FromResult(new Quote(request.Sku, 9.99m));
                }));

            await harness.Client.RequestAsync(new GetQuote("sku-1"), Timeout, Ct);

            var token = await handlerToken.Task.WaitAsync(Timeout, Ct);
            Assert.Equal(token, recording.Single(r => r.Operation == "reply").Token);
        }

        private static Task<ObvsTestHarness> StartOrdersAsync(ConcurrentQueue<(string, IMessage, CancellationToken)> recording,
            Action<ObvsServiceBuilder> configure, bool shipping = false)
        {
            return ObvsTestHarness.StartAsync(services =>
            {
                services.AddObvs(obvs =>
                {
                    obvs.AddService<IOrderMessage>("orders", service => configure(service.UseInMemory()));
                    if (shipping)
                    {
                        obvs.AddService<IShippingMessage>("shipping", service => service.UseInMemory());
                    }
                });
                RecordBusCalls(services, recording);
            }, cancellationToken: Ct);
        }

        // Swaps the bus AddObvs registered for one that records what's sent through it and with which token
        private static void RecordBusCalls(IServiceCollection services, ConcurrentQueue<(string, IMessage, CancellationToken)> recording)
        {
            var registered = services.Single(descriptor => descriptor.ServiceType == typeof(IServiceBus) && !descriptor.IsKeyedService);
            services.Remove(registered);
            services.AddSingleton<IServiceBus>(serviceProvider => new RecordingServiceBus((IServiceBus)registered.ImplementationFactory(serviceProvider), recording));
        }

        private sealed class RecordingServiceBus : IServiceBus, IDisposable
        {
            private readonly IServiceBus _inner;
            private readonly ConcurrentQueue<(string, IMessage, CancellationToken)> _recording;

            public RecordingServiceBus(IServiceBus inner, ConcurrentQueue<(string, IMessage, CancellationToken)> recording)
            {
                _inner = inner;
                _recording = recording;
            }

            public IObservable<IEvent> Events => _inner.Events;
            public IObservable<IRequest> Requests => _inner.Requests;
            public IObservable<ICommand> Commands => _inner.Commands;
            public IObservable<Exception> Exceptions => _inner.Exceptions;

            public Task SendAsync(ICommand command, CancellationToken cancellationToken = default)
            {
                _recording.Enqueue(("send", command, cancellationToken));
                return _inner.SendAsync(command, cancellationToken);
            }

            public Task SendAsync(ICommand command, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default)
            {
                _recording.Enqueue(("send", command, cancellationToken));
                return _inner.SendAsync(command, headers, cancellationToken);
            }

            public Task SendAsync(IEnumerable<ICommand> commands, CancellationToken cancellationToken = default)
            {
                return _inner.SendAsync(commands, cancellationToken);
            }

            public Task PublishAsync(IEvent ev, CancellationToken cancellationToken = default)
            {
                _recording.Enqueue(("publish", ev, cancellationToken));
                return _inner.PublishAsync(ev, cancellationToken);
            }

            public Task PublishAsync(IEvent ev, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default)
            {
                _recording.Enqueue(("publish", ev, cancellationToken));
                return _inner.PublishAsync(ev, headers, cancellationToken);
            }

            public Task ReplyAsync(IRequest request, IResponse response, CancellationToken cancellationToken = default)
            {
                _recording.Enqueue(("reply", response, cancellationToken));
                return _inner.ReplyAsync(request, response, cancellationToken);
            }

            public IObservable<IResponse> GetResponses(IRequest request) => _inner.GetResponses(request);
            public IObservable<IResponse> GetResponses(IRequest request, IReadOnlyDictionary<string, string> headers) => _inner.GetResponses(request, headers);
            public Task<TResponse> RequestAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default) where TResponse : class, IResponse => _inner.RequestAsync(request, cancellationToken);
            public Task<TResponse> RequestAsync<TResponse>(IRequest<TResponse> request, TimeSpan timeout, CancellationToken cancellationToken = default) where TResponse : class, IResponse => _inner.RequestAsync(request, timeout, cancellationToken);
            public Task<TResponse> RequestAsync<TResponse>(IRequest<TResponse> request, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default) where TResponse : class, IResponse => _inner.RequestAsync(request, headers, cancellationToken);
            public Task<TResponse> RequestAsync<TResponse>(IRequest<TResponse> request, IReadOnlyDictionary<string, string> headers, TimeSpan timeout, CancellationToken cancellationToken = default) where TResponse : class, IResponse => _inner.RequestAsync(request, headers, timeout, cancellationToken);
            public IAsyncEnumerable<TEvent> ReadEventsAsync<TEvent>(CancellationToken cancellationToken = default, int capacity = 1024) where TEvent : IEvent => _inner.ReadEventsAsync<TEvent>(cancellationToken, capacity);
            public IAsyncEnumerable<TCommand> ReadCommandsAsync<TCommand>(CancellationToken cancellationToken = default, int capacity = 1024) where TCommand : ICommand => _inner.ReadCommandsAsync<TCommand>(cancellationToken, capacity);

            public void Dispose() => ((IDisposable)_inner).Dispose();
        }
    }
}
