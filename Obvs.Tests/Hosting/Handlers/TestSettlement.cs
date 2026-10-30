using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Obvs.Configuration;
using Obvs.Hosting;
using Obvs.Transports;
using Obvs.Types;
using Xunit;

namespace Obvs.Tests.Hosting.Handlers
{
    // Handlers settle messages from transports whose broker keeps a message until it's settled
    public class TestSettlement
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        [Fact]
        public async Task ShouldCompleteAMessageWhenItsHandlerSucceeds()
        {
            var transport = new SettlingTransport();
            using var host = await StartHostAsync(transport, service => service
                .HandleCommand<PlaceOrder>((command, context, ct) => Task.CompletedTask));

            transport.Deliver(new PlaceOrder("o-1"));

            Assert.Equal(new[] { "complete o-1" }, await transport.WaitForAsync(1));
        }

        [Fact]
        public async Task ShouldAbandonAMessageWithTheExceptionWhenItsHandlerFails()
        {
            var transport = new SettlingTransport();
            using var host = await StartHostAsync(transport, service => service
                .HandleCommand<PlaceOrder>((command, context, ct) => throw new InvalidOperationException("Out of stock")));

            transport.Deliver(new PlaceOrder("o-1"));

            Assert.Equal(new[] { "abandon o-1: Out of stock" }, await transport.WaitForAsync(1));
        }

        [Fact]
        public async Task ShouldCompleteARequestAfterItsReplyIsSent()
        {
            var transport = new SettlingTransport();
            using var host = await StartHostAsync(transport, service => service
                .HandleRequest<GetQuote, Quote>((request, context, ct) => Task.FromResult(new Quote(request.Sku, 9.99m))));

            transport.Deliver(new GetQuote("sku-1") { RequestId = "r-1", RequesterId = "client" });

            Assert.Equal(new[] { "reply sku-1", "complete sku-1" }, await transport.WaitForAsync(2));
        }

        [Fact]
        public async Task ShouldCompleteMessagesNoHandlerHandlesWhenTheyArrive()
        {
            // Subscribers to the bus's streams don't settle messages, so they're completed on arrival as before
            var transport = new SettlingTransport();
            using var host = await StartHostAsync(transport, service => service
                .HandleRequest<GetQuote, Quote>((request, context, ct) => Task.FromResult(new Quote(request.Sku, 9.99m))));
            var received = new TaskCompletionSource<PlaceOrder>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var subscription = host.Services.GetRequiredService<IServiceBus>().Commands.OfType<PlaceOrder>().Subscribe(command => received.TrySetResult(command));

            transport.Deliver(new PlaceOrder("o-1"));

            Assert.Equal(new[] { "complete o-1" }, await transport.WaitForAsync(1));
            Assert.Equal("o-1", (await received.Task.WaitAsync(Timeout, Ct)).OrderId);
        }

        [Fact]
        public async Task ShouldLetARunningHandlerFinishAndSettleBeforeTheTransportStops()
        {
            var transport = new SettlingTransport();
            var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var finish = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var host = await StartHostAsync(transport, service => service
                .HandleCommand<PlaceOrder>(async (command, context, ct) =>
                {
                    started.TrySetResult(true);
                    await finish.Task;
                }));
            transport.Deliver(new PlaceOrder("o-1"));
            await started.Task.WaitAsync(Timeout, Ct);

            var stopping = host.StopAsync(Ct);
            finish.SetResult(true);
            await stopping.WaitAsync(Timeout, Ct);

            Assert.Equal(new[] { "complete o-1", "commands stopped" }, await transport.WaitForAsync(2));
        }

        [Fact]
        public async Task ShouldReleaseMessagesLeftUnhandledWhenStoppingRunsOutOfTime()
        {
            var transport = new SettlingTransport();
            var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var host = await StartHostAsync(transport, service => service
                .HandleCommand<PlaceOrder>(async (command, context, ct) =>
                {
                    started.TrySetResult(true);
                    await Task.Delay(System.Threading.Timeout.Infinite, ct);
                }));
            transport.Deliver(new PlaceOrder("o-1"));
            transport.Deliver(new PlaceOrder("o-2"));
            await started.Task.WaitAsync(Timeout, Ct);

            using var outOfTime = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
            await host.StopAsync(outOfTime.Token);

            // o-1 was cancelled mid-handler and o-2 never started; neither failed, so both go back to the broker
            var settled = await transport.WaitForAsync(3);
            Assert.Equal(new[] { "release o-1", "release o-2" }, settled.Where(entry => entry.StartsWith("release")).OrderBy(entry => entry));
        }

        [Fact]
        public async Task ShouldReleaseMessagesThatArriveWhileTheHostStops()
        {
            var transport = new SettlingTransport();
            var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var finish = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var host = await StartHostAsync(transport, service => service
                .HandleCommand<PlaceOrder>(async (command, context, ct) =>
                {
                    started.TrySetResult(true);
                    await finish.Task;
                }));
            transport.Deliver(new PlaceOrder("o-1"));
            await started.Task.WaitAsync(Timeout, Ct);

            var stopping = host.StopAsync(Ct);
            await WaitUntilStoppingAsync(host);
            transport.Deliver(new PlaceOrder("o-2"));
            finish.SetResult(true);
            await stopping.WaitAsync(Timeout, Ct);

            Assert.Equal(new[] { "release o-2", "complete o-1", "commands stopped" }, await transport.WaitForAsync(3));
        }

        [Fact]
        public async Task ShouldLogAndCarryOnWhenSettlingAMessageFails()
        {
            var logs = new ListLoggerProvider();
            var transport = new SettlingTransport { FailToSettle = "o-1" };
            using var host = await StartHostAsync(transport, service => service
                .HandleCommand<PlaceOrder>((command, context, ct) => Task.CompletedTask), logs);

            transport.Deliver(new PlaceOrder("o-1"));
            transport.Deliver(new PlaceOrder("o-2"));

            Assert.Equal(new[] { "complete o-1", "complete o-2" }, await transport.WaitForAsync(2));
            var error = Assert.Single(logs.Entries, entry => entry.Level == LogLevel.Error && entry.Message.Contains("settl"));
            Assert.Contains("PlaceOrder", error.Message);
            Assert.Contains("'orders'", error.Message);
        }

        // The host signals stopping before it stops its hosted services, so allow a moment for that to reach the handlers
        private static async Task WaitUntilStoppingAsync(IHost host)
        {
            await Task.Delay(System.Threading.Timeout.Infinite, host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping)
                .ContinueWith(_ => { }, TaskScheduler.Default).WaitAsync(Timeout, Ct);
            await Task.Delay(100, Ct);
        }

        private static async Task<IHost> StartHostAsync(SettlingTransport transport, Action<ObvsServiceBuilder> configure, ILoggerProvider logs = null)
        {
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
            if (logs != null)
            {
                builder.Logging.AddProvider(logs);
            }
            builder.Services.AddObvs(obvs => obvs.AddService<IOrderMessage>("orders", service =>
            {
                service.UseEndpointProvider(_ => transport);
                configure(service);
            }));
            var host = builder.Build();
            await host.StartAsync(Ct);
            return host;
        }

        // A transport whose messages wait to be settled. It records each settlement, each reply, and when its
        // command stream stops, in the order they happen.
        private sealed class SettlingTransport : IServiceEndpointProvider<IMessage, ICommand, IEvent, IRequest, IResponse>
        {
            private readonly Subject<ICommand> _commands = new Subject<ICommand>();
            private readonly Subject<IRequest> _requests = new Subject<IRequest>();
            private readonly ConcurrentQueue<string> _log = new ConcurrentQueue<string>();

            public string FailToSettle { get; set; }

            public void Deliver(IMessage message)
            {
                var id = Id(message);
                ObvsReceivedMessage.Attach(message, new Dictionary<string, string>(), new Settlement(this, id));
                switch (message)
                {
                    case ICommand command:
                        _commands.OnNext(command);
                        break;
                    case IRequest request:
                        _requests.OnNext(request);
                        break;
                }
            }

            public async Task<string[]> WaitForAsync(int count)
            {
                var deadline = DateTime.UtcNow + Timeout;
                while (_log.Count < count && DateTime.UtcNow < deadline)
                {
                    await Task.Delay(10, Ct);
                }
                await Task.Delay(100, Ct); // Anything more that's going to happen
                return _log.ToArray();
            }

            public IServiceEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse> CreateEndpoint()
            {
                var commands = Observable.Create<ICommand>(observer =>
                {
                    var subscription = _commands.Subscribe(observer);
                    return Disposable.Create(() =>
                    {
                        subscription.Dispose();
                        _log.Enqueue("commands stopped");
                    });
                });
                return new ServiceEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse>(
                    new Source<IRequest>(_requests), new Source<ICommand>(commands),
                    new DefaultMessagePublisher<IEvent>(), new Publisher<IResponse>(response => _log.Enqueue($"reply {Id(response)}")),
                    typeof(IOrderMessage));
            }

            public IServiceEndpointClient<IMessage, ICommand, IEvent, IRequest, IResponse> CreateEndpointClient()
            {
                return new ServiceEndpointClient<IMessage, ICommand, IEvent, IRequest, IResponse>(
                    new Source<IEvent>(Observable.Never<IEvent>()), new Source<IResponse>(Observable.Never<IResponse>()),
                    new DefaultMessagePublisher<IRequest>(), new DefaultMessagePublisher<ICommand>(),
                    typeof(IOrderMessage));
            }

            private static string Id(IMessage message) => message switch
            {
                PlaceOrder placeOrder => placeOrder.OrderId,
                GetQuote getQuote => getQuote.Sku,
                Quote quote => quote.Sku,
                _ => message.GetType().Name,
            };

            private sealed class Settlement : IMessageSettlement
            {
                private readonly SettlingTransport _transport;
                private readonly string _id;

                public Settlement(SettlingTransport transport, string id)
                {
                    _transport = transport;
                    _id = id;
                }

                public Task CompleteAsync() => Record($"complete {_id}");

                public Task AbandonAsync(Exception exception) => Record($"abandon {_id}: {exception.Message}");

                public Task ReleaseAsync() => Record($"release {_id}");

                private Task Record(string entry)
                {
                    _transport._log.Enqueue(entry);
                    return _id == _transport.FailToSettle
                        ? Task.FromException(new InvalidOperationException("The broker lost the message's lock."))
                        : Task.CompletedTask;
                }
            }

            private sealed class Source<T> : IMessageSource<T> where T : class
            {
                public Source(IObservable<T> messages) => Messages = messages;

                public IObservable<T> Messages { get; }

                public void Dispose()
                {
                }
            }

            private sealed class Publisher<T> : IMessagePublisher<T> where T : class
            {
                private readonly Action<T> _publish;

                public Publisher(Action<T> publish) => _publish = publish;

                public Task PublishAsync(T message, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default)
                {
                    _publish(message);
                    return Task.CompletedTask;
                }

                public void Dispose()
                {
                }
            }
        }
    }
}
