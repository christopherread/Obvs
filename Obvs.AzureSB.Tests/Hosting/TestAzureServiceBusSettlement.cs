using System;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Obvs.Hosting;
using Obvs.Serialization.Json;
using Xunit;

namespace Obvs.AzureSB.Tests.Hosting
{
    // Messages a handler handles stay on Service Bus until the handler settles them (see TestSettlement in Obvs.Tests)
    public class TestAzureServiceBusSettlement : IClassFixture<TestAzureServiceBusHosting.ServiceNames>, IAsyncDisposable
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);
        private readonly TestAzureServiceBusHosting.ServiceNames _serviceNames;

        public TestAzureServiceBusSettlement(TestAzureServiceBusHosting.ServiceNames serviceNames)
        {
            _serviceNames = serviceNames;
        }

        public ValueTask DisposeAsync() => _serviceNames.DeleteCreatedAsync();

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldKeepACommandOnItsQueueUntilItsHandlerSucceeds()
        {
            var serviceName = _serviceNames.Create("settle-on-success");
            var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var finish = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var host = await StartHostAsync(serviceName, service => service
                .HandleCommand<PlaceOrder>(async (command, context, ct) =>
                {
                    started.TrySetResult(true);
                    await finish.Task;
                }));

            await host.Services.GetRequiredService<IServiceBus>().SendAsync(new PlaceOrder { OrderId = "o-1" }, TestContext.Current.CancellationToken);
            await started.Task.WaitAsync(Timeout, Ct);

            Assert.Equal(1, await MessagesOnQueueAsync($"{serviceName}.Commands"));
            finish.SetResult(true);
            await WaitUntilAsync(async () => await MessagesOnQueueAsync($"{serviceName}.Commands") == 0);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldRedeliverACommandWhoseHandlerFails()
        {
            var serviceName = _serviceNames.Create("redeliver");
            var attempts = 0;
            using var host = await StartHostAsync(serviceName, service => service
                .HandleCommand<PlaceOrder>((command, context, ct) => Interlocked.Increment(ref attempts) == 1
                    ? throw new InvalidOperationException("Out of stock")
                    : Task.CompletedTask));

            await host.Services.GetRequiredService<IServiceBus>().SendAsync(new PlaceOrder { OrderId = "o-1" }, TestContext.Current.CancellationToken);

            await WaitUntilAsync(() => Task.FromResult(Volatile.Read(ref attempts) == 2));
            await WaitUntilAsync(async () => await MessagesOnQueueAsync($"{serviceName}.Commands") == 0);
            await Task.Delay(TimeSpan.FromSeconds(2), Ct);
            Assert.Equal(2, Volatile.Read(ref attempts));
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldRedeliverACommandWhoseSubscribeAsyncFails()
        {
            var serviceName = _serviceNames.Create("redeliver-rx");
            var attempts = 0;
            using var host = await StartHostAsync(serviceName);
            var bus = host.Services.GetRequiredService<IServiceBus>();
            using var subscription = bus.Commands.OfType<PlaceOrder>().SubscribeAsync((command, ct) => Interlocked.Increment(ref attempts) == 1
                ? throw new InvalidOperationException("Out of stock")
                : Task.CompletedTask);

            await bus.SendAsync(new PlaceOrder { OrderId = "o-1" }, TestContext.Current.CancellationToken);

            await WaitUntilAsync(() => Task.FromResult(Volatile.Read(ref attempts) == 2));
            await WaitUntilAsync(async () => await MessagesOnQueueAsync($"{serviceName}.Commands") == 0);
            await Task.Delay(TimeSpan.FromSeconds(2), Ct);
            Assert.Equal(2, Volatile.Read(ref attempts));
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldDeadLetterACommandAfterMaxDeliveryAttempts()
        {
            var serviceName = _serviceNames.Create("dead-letter");
            var attempts = 0;
            using var host = await StartHostAsync(serviceName, service => service
                .HandleCommand<PlaceOrder>((command, context, ct) =>
                {
                    Interlocked.Increment(ref attempts);
                    throw new InvalidOperationException("Out of stock");
                }), maxDeliveryAttempts: 2);

            await host.Services.GetRequiredService<IServiceBus>().SendAsync(new PlaceOrder { OrderId = "o-1" }, TestContext.Current.CancellationToken);

            await using var client = TestBroker.CreateClient();
            await using var deadLetters = client.CreateReceiver($"{serviceName}.Commands", new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });
            var deadLetter = await deadLetters.ReceiveMessageAsync(Timeout, Ct);
            Assert.NotNull(deadLetter);
            Assert.Equal(2, Volatile.Read(ref attempts));
            Assert.Equal("HandlerFailed", deadLetter.DeadLetterReason);
            Assert.Contains("Out of stock", deadLetter.DeadLetterErrorDescription);
            Assert.Equal("System.InvalidOperationException", deadLetter.ApplicationProperties["obvs-exception-type"]);
            Assert.Equal(2, deadLetter.DeliveryCount);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldRemoveCommandsNoHandlerHandlesWhenTheyArrive()
        {
            var serviceName = _serviceNames.Create("no-handler");
            using var host = await StartHostAsync(serviceName);
            var bus = host.Services.GetRequiredService<IServiceBus>();
            var received = bus.Commands.OfType<PlaceOrder>().FirstAsync().ToTask();

            await bus.SendAsync(new PlaceOrder { OrderId = "o-1" }, TestContext.Current.CancellationToken);

            Assert.Equal("o-1", (await received.WaitAsync(Timeout, Ct)).OrderId);
            await WaitUntilAsync(async () => await MessagesOnQueueAsync($"{serviceName}.Commands") == 0);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldFinishAndCompleteARunningCommandWhenTheHostStops()
        {
            var serviceName = _serviceNames.Create("stop");
            var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var finish = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var host = await StartHostAsync(serviceName, service => service
                .HandleCommand<PlaceOrder>(async (command, context, ct) =>
                {
                    started.TrySetResult(true);
                    await finish.Task;
                }));
            await host.Services.GetRequiredService<IServiceBus>().SendAsync(new PlaceOrder { OrderId = "o-1" }, TestContext.Current.CancellationToken);
            await started.Task.WaitAsync(Timeout, Ct);

            var stopping = host.StopAsync(Ct);
            finish.SetResult(true);
            await stopping.WaitAsync(Timeout, Ct);
            host.Dispose();

            Assert.Equal(0, await MessagesOnQueueAsync($"{serviceName}.Commands"));
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldCreateEntitiesWithMaxDeliveryAttemptsAsTheirDeliveryLimit()
        {
            var serviceName = _serviceNames.Create("delivery-limit");
            const string instanceId = "orders-worker-1";

            using var host = await StartHostAsync(serviceName, maxDeliveryAttempts: 3, instanceId: instanceId);

            var admin = TestBroker.CreateAdministrationClient();
            Assert.Equal(3, (await admin.GetQueueAsync($"{serviceName}.Commands", Ct)).Value.MaxDeliveryCount);
            Assert.Equal(3, (await admin.GetQueueAsync($"{serviceName}.Requests", Ct)).Value.MaxDeliveryCount);
            Assert.Equal(3, (await admin.GetSubscriptionAsync($"{serviceName}.Events", instanceId, Ct)).Value.MaxDeliveryCount);
        }

        // Peeking sees every message still on the queue, locked by a receiver or not. (The emulator doesn't keep the
        // queue's runtime counts, which stay at 0.)
        private static async Task<int> MessagesOnQueueAsync(string queue)
        {
            await using var client = TestBroker.CreateClient();
            await using var receiver = client.CreateReceiver(queue);
            return (await receiver.PeekMessagesAsync(10, cancellationToken: Ct)).Count;
        }

        private static async Task WaitUntilAsync(Func<Task<bool>> condition)
        {
            var deadline = DateTime.UtcNow + Timeout;
            while (!await condition())
            {
                if (DateTime.UtcNow > deadline)
                {
                    throw new TimeoutException("The condition wasn't met in time.");
                }
                await Task.Delay(200, Ct);
            }
        }

        private static async Task<IHost> StartHostAsync(string serviceName, Action<ObvsServiceBuilder> configure = null, int maxDeliveryAttempts = 10, string instanceId = null)
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
                            options.MaxDeliveryAttempts = maxDeliveryAttempts;
                        })
                        .SerializedWith(new JsonMessageSerializer(), new JsonMessageDeserializerFactory(typeof(JsonMessageDeserializer<>)));
                    configure?.Invoke(service);
                });
            });
            var host = builder.Build();
            await host.StartAsync(Ct);
            return host;
        }
    }
}
