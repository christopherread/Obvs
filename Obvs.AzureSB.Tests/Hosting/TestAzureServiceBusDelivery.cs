using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Obvs.Hosting;
using Obvs.Serialization.Json;
using Xunit;

namespace Obvs.AzureSB.Tests.Hosting
{
    // How many messages a bus handles at once, and how long it keeps a message locked while a handler runs
    public class TestAzureServiceBusDelivery : IClassFixture<TestAzureServiceBusHosting.ServiceNames>, IAsyncDisposable
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);
        private readonly TestAzureServiceBusHosting.ServiceNames _serviceNames;

        public TestAzureServiceBusDelivery(TestAzureServiceBusHosting.ServiceNames serviceNames)
        {
            _serviceNames = serviceNames;
        }

        public ValueTask DisposeAsync() => _serviceNames.DeleteCreatedAsync();

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldHandleUpToMaxConcurrentCallsCommandsAtOnce()
        {
            var running = new ConcurrencyRecorder();
            using var host = await StartHostAsync(_serviceNames.Create("concurrent"), options => options.MaxConcurrentCalls = 3,
                service => service.HandleCommand<PlaceOrder>((command, context, ct) => running.RunAsync(), handler => handler.MaxConcurrency = 3));
            var bus = host.Services.GetRequiredService<IServiceBus>();

            for (var i = 0; i < 3; i++)
            {
                await bus.SendAsync(new PlaceOrder { OrderId = $"o-{i}" }, TestContext.Current.CancellationToken);
            }

            await WaitUntilAsync(() => running.Current == 3);
            running.Finish();
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldHandleOneCommandAtATimeByDefault()
        {
            var running = new ConcurrencyRecorder();
            using var host = await StartHostAsync(_serviceNames.Create("one-at-a-time"), options => { },
                service => service.HandleCommand<PlaceOrder>((command, context, ct) => running.RunAsync(), handler => handler.MaxConcurrency = 3));
            var bus = host.Services.GetRequiredService<IServiceBus>();

            for (var i = 0; i < 3; i++)
            {
                await bus.SendAsync(new PlaceOrder { OrderId = $"o-{i}" }, TestContext.Current.CancellationToken);
            }

            await WaitUntilAsync(() => running.Current == 1);
            await Task.Delay(TimeSpan.FromSeconds(3), Ct);
            Assert.Equal(1, running.Most);
            running.Finish();
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldReturnTheResponseToEachOfABusesConcurrentRequests()
        {
            // Every request in flight listens on the bus's one reply queue
            var serviceName = _serviceNames.Create("concurrent-requests");
            using var server = await StartHostAsync(serviceName, options => options.MaxConcurrentCalls = 10,
                service => service.AsServer().HandleRequest<GetQuote, Quote>(async (request, context, ct) =>
                {
                    await Task.Delay(200, ct);
                    return new Quote { Price = decimal.Parse(request.Sku) };
                }, handler => handler.MaxConcurrency = 10));
            using var client = await StartHostAsync(serviceName, options => { }, service => service.AsClient());
            var bus = client.Services.GetRequiredService<IServiceBusClient>();

            var quotes = await Task.WhenAll(Enumerable.Range(1, 10).Select(i => bus.RequestAsync<GetQuote, Quote>(new GetQuote { Sku = $"{i}" }, Timeout, Ct)));

            Assert.Equal(Enumerable.Range(1, 10).Select(i => (decimal)i), quotes.Select(quote => quote.Price));
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldCreateEntitiesWithTheLockDuration()
        {
            var serviceName = _serviceNames.Create("lock-duration");
            const string instanceId = "orders-worker-1";

            using var host = await StartHostAsync(serviceName, options => options.LockDuration = TimeSpan.FromSeconds(5), instanceId: instanceId);

            var admin = TestBroker.CreateAdministrationClient();
            Assert.Equal(TimeSpan.FromSeconds(5), (await admin.GetQueueAsync($"{serviceName}.Commands", Ct)).Value.LockDuration);
            Assert.Equal(TimeSpan.FromSeconds(5), (await admin.GetQueueAsync($"{serviceName}.Requests", Ct)).Value.LockDuration);
            Assert.Equal(TimeSpan.FromSeconds(5), (await admin.GetSubscriptionAsync($"{serviceName}.Events", instanceId, Ct)).Value.LockDuration);
            Assert.Equal(TimeSpan.FromSeconds(5), (await admin.GetQueueAsync($"{serviceName}.Responses.{instanceId}", Ct)).Value.LockDuration);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldKeepRenewingTheLockWhileAHandlerRunsPastTheLockDuration()
        {
            var serviceName = _serviceNames.Create("renew-lock");
            var attempts = 0;
            using var host = await StartHostAsync(serviceName, options => options.LockDuration = TimeSpan.FromSeconds(5),
                service => service.HandleCommand<PlaceOrder>(async (command, context, ct) =>
                {
                    Interlocked.Increment(ref attempts);
                    await Task.Delay(TimeSpan.FromSeconds(12), ct);
                }));

            await host.Services.GetRequiredService<IServiceBus>().SendAsync(new PlaceOrder { OrderId = "o-1" }, TestContext.Current.CancellationToken);

            await WaitUntilAsync(() => Volatile.Read(ref attempts) == 1);
            await Task.Delay(TimeSpan.FromSeconds(16), Ct);
            Assert.Equal(1, Volatile.Read(ref attempts));
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldRedeliverACommandWhoseHandlerOutlastsMaxLockRenewalDuration()
        {
            var serviceName = _serviceNames.Create("lock-lost");
            var attempts = 0;
            using var host = await StartHostAsync(serviceName, options =>
                {
                    options.LockDuration = TimeSpan.FromSeconds(5);
                    options.MaxLockRenewalDuration = TimeSpan.Zero;
                },
                service => service.HandleCommand<PlaceOrder>(async (command, context, ct) =>
                {
                    if (Interlocked.Increment(ref attempts) == 1)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(12), ct);
                    }
                }));

            await host.Services.GetRequiredService<IServiceBus>().SendAsync(new PlaceOrder { OrderId = "o-1" }, TestContext.Current.CancellationToken);

            await WaitUntilAsync(() => Volatile.Read(ref attempts) >= 2);
        }

        private static async Task WaitUntilAsync(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow + Timeout;
            while (!condition())
            {
                if (DateTime.UtcNow > deadline)
                {
                    throw new TimeoutException("The condition wasn't met in time.");
                }
                await Task.Delay(100, Ct);
            }
        }

        private static async Task<IHost> StartHostAsync(string serviceName, Action<AzureServiceBusOptions> configureOptions,
            Action<ObvsServiceBuilder> configure = null, string instanceId = null)
        {
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
            builder.Services.AddObvs(obvs =>
            {
                if (instanceId != null)
                {
                    obvs.UseInstanceId(instanceId);
                }
                obvs.AddService<IOrderMessage>(serviceName, service =>
                {
                    service
                        .UseAzureServiceBus(options =>
                        {
                            options.ConnectionString = TestBroker.ConnectionString;
                            configureOptions(options);
                        })
                        .SerializedWith(new JsonMessageSerializer(), new JsonMessageDeserializerFactory(typeof(JsonMessageDeserializer<>)));
                    configure?.Invoke(service);
                });
            });
            var host = builder.Build();
            await host.StartAsync(Ct);
            return host;
        }

        // Counts handlers running at once, holding each until Finish
        private sealed class ConcurrencyRecorder
        {
            private readonly TaskCompletionSource<bool> _finish = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            private int _current;
            private int _most;

            public int Current => Volatile.Read(ref _current);

            public int Most => Volatile.Read(ref _most);

            public async Task RunAsync()
            {
                var now = Interlocked.Increment(ref _current);
                int most;
                while ((most = Volatile.Read(ref _most)) < now && Interlocked.CompareExchange(ref _most, now, most) != most)
                {
                }
                try
                {
                    await _finish.Task;
                }
                finally
                {
                    Interlocked.Decrement(ref _current);
                }
            }

            public void Finish() => _finish.TrySetResult(true);
        }
    }
}
