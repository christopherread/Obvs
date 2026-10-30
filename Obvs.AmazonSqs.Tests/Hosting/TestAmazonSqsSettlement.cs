using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SQS.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Obvs.Hosting;
using Obvs.Transports;
using Xunit;

namespace Obvs.AmazonSqs.Tests.Hosting
{
    // Messages a handler handles stay on SQS until the handler settles them (see TestSettlement in Obvs.Tests)
    public class TestAmazonSqsSettlement : IClassFixture<TestAmazonSqsHosting.ServiceNames>
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);
        private readonly TestAmazonSqsHosting.ServiceNames _serviceNames;

        public TestAmazonSqsSettlement(TestAmazonSqsHosting.ServiceNames serviceNames)
        {
            _serviceNames = serviceNames;
        }

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldKeepACommandOnItsQueueUntilItsHandlerSucceeds()
        {
            var serviceName = _serviceNames.Create("settle-on-success");
            var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var finish = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var host = await TestAmazonSqsHosting.StartHostAsync(serviceName, service => service
                .HandleCommand<PlaceOrder>(async (command, context, ct) =>
                {
                    started.TrySetResult(true);
                    await finish.Task;
                }));

            await host.Services.GetRequiredService<IServiceBus>().SendAsync(new PlaceOrder { OrderId = "o-1" }, TestContext.Current.CancellationToken);
            await started.Task.WaitAsync(Timeout, Ct);

            Assert.Equal(1, await TestBroker.MessagesOnQueueAsync($"{serviceName}-commands", Ct));
            finish.SetResult(true);
            await WaitUntilAsync(async () => await TestBroker.MessagesOnQueueAsync($"{serviceName}-commands", Ct) == 0);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldRedeliverACommandWhoseHandlerFails()
        {
            // Straight away, rather than once the visibility timeout passes
            var serviceName = _serviceNames.Create("redeliver");
            var attempts = 0;
            using var host = await TestAmazonSqsHosting.StartHostAsync(serviceName, service => service
                .HandleCommand<PlaceOrder>((command, context, ct) => Interlocked.Increment(ref attempts) == 1
                    ? throw new InvalidOperationException("Out of stock")
                    : Task.CompletedTask),
                configureOptions: options => options.VisibilityTimeout = TimeSpan.FromMinutes(5));

            await host.Services.GetRequiredService<IServiceBus>().SendAsync(new PlaceOrder { OrderId = "o-1" }, TestContext.Current.CancellationToken);

            await WaitUntilAsync(() => Task.FromResult(Volatile.Read(ref attempts) == 2));
            await WaitUntilAsync(async () => await TestBroker.MessagesOnQueueAsync($"{serviceName}-commands", Ct) == 0);
            await Task.Delay(TimeSpan.FromSeconds(2), Ct);
            Assert.Equal(2, Volatile.Read(ref attempts));
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldDeadLetterACommandAfterMaxDeliveryAttempts()
        {
            var serviceName = _serviceNames.Create("dead-letter");
            var attempts = 0;
            using var host = await TestAmazonSqsHosting.StartHostAsync(serviceName, service => service
                .HandleCommand<PlaceOrder>((command, context, ct) =>
                {
                    Interlocked.Increment(ref attempts);
                    throw new InvalidOperationException("Out of stock");
                }), configureOptions: options => options.MaxDeliveryAttempts = 2);

            await host.Services.GetRequiredService<IServiceBus>().SendAsync(new PlaceOrder { OrderId = "o-1" }, TestContext.Current.CancellationToken);

            var deadLetter = await ReceiveDeadLetterAsync(serviceName);
            Assert.Equal(2, Volatile.Read(ref attempts));
            Assert.Contains("o-1", deadLetter.Body);
            Assert.Equal("PlaceOrder", deadLetter.MessageAttributes[MessagePropertyNames.TypeName].StringValue);
            Assert.Equal("HandlerFailed", deadLetter.MessageAttributes["obvs-dead-letter-reason"].StringValue);
            Assert.Contains("Out of stock", deadLetter.MessageAttributes["obvs-dead-letter-description"].StringValue);
            Assert.Equal("System.InvalidOperationException", deadLetter.MessageAttributes["obvs-exception-type"].StringValue);
            Assert.Equal("2", deadLetter.MessageAttributes["obvs-delivery-attempts"].StringValue);
            await WaitUntilAsync(async () => await TestBroker.MessagesOnQueueAsync($"{serviceName}-commands", Ct) == 0);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldDeadLetterAMessageThatCantBeDeserializedThroughTheRedrivePolicy()
        {
            // No handler gets to settle it, so it's SQS's redrive policy that moves it, after MaxDeliveryAttempts receives
            var serviceName = _serviceNames.Create("poison");
            using var host = await TestAmazonSqsHosting.StartHostAsync(serviceName, service => service
                .HandleCommand<PlaceOrder>((command, context, ct) => Task.CompletedTask), configureOptions: options => options.MaxDeliveryAttempts = 2);
            using var sqs = TestBroker.CreateSqsClient();

            await sqs.SendMessageAsync(new SendMessageRequest
            {
                QueueUrl = await TestBroker.QueueUrlAsync($"{serviceName}-commands", Ct),
                MessageBody = "not json",
                MessageAttributes = new Dictionary<string, MessageAttributeValue>
                {
                    [MessagePropertyNames.TypeName] = new MessageAttributeValue { DataType = "String", StringValue = nameof(PlaceOrder) },
                },
            }, Ct);

            var deadLetter = await ReceiveDeadLetterAsync(serviceName);
            Assert.Equal("not json", deadLetter.Body);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldRemoveCommandsNoHandlerHandlesWhenTheyArrive()
        {
            var serviceName = _serviceNames.Create("no-handler");
            using var host = await TestAmazonSqsHosting.StartHostAsync(serviceName);
            var bus = host.Services.GetRequiredService<IServiceBus>();
            var received = bus.Commands.OfType<PlaceOrder>().FirstAsync().ToTask();

            await bus.SendAsync(new PlaceOrder { OrderId = "o-1" }, TestContext.Current.CancellationToken);

            Assert.Equal("o-1", (await received.WaitAsync(Timeout, Ct)).OrderId);
            await WaitUntilAsync(async () => await TestBroker.MessagesOnQueueAsync($"{serviceName}-commands", Ct) == 0);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldRemoveEventsAndResponsesFromTheBusesOwnQueues()
        {
            var serviceName = _serviceNames.Create("own-queues");
            const string instanceId = "orders-worker-1";
            using var host = await TestAmazonSqsHosting.StartHostAsync(serviceName, service => service
                .HandleRequest<GetQuote, Quote>((request, context, ct) => Task.FromResult(new Quote { Price = 1m })), instanceId: instanceId);
            var bus = host.Services.GetRequiredService<IServiceBus>();
            var placed = bus.Events.OfType<OrderPlaced>().FirstAsync().ToTask();

            await bus.PublishAsync(new OrderPlaced { OrderId = "o-1" }, TestContext.Current.CancellationToken);
            await bus.RequestAsync(new GetQuote { Sku = "sku-1" }, Timeout, Ct);
            await placed.WaitAsync(Timeout, Ct);

            await WaitUntilAsync(async () => await TestBroker.MessagesOnQueueAsync($"{serviceName}-events-{instanceId}", Ct) == 0);
            await WaitUntilAsync(async () => await TestBroker.MessagesOnQueueAsync($"{serviceName}-responses-{instanceId}", Ct) == 0);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldFinishAndCompleteARunningCommandWhenTheHostStops()
        {
            var serviceName = _serviceNames.Create("stop");
            var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var finish = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var host = await TestAmazonSqsHosting.StartHostAsync(serviceName, service => service
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

            Assert.Equal(0, await TestBroker.MessagesOnQueueAsync($"{serviceName}-commands", Ct));
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldCreateQueuesThatDeadLetterAfterMaxDeliveryAttempts()
        {
            var serviceName = _serviceNames.Create("redrive");
            const string instanceId = "orders-worker-1";

            using var host = await TestAmazonSqsHosting.StartHostAsync(serviceName, instanceId: instanceId, configureOptions: options => options.MaxDeliveryAttempts = 3);

            var deadLetterArn = (await TestBroker.QueueAttributesAsync($"{serviceName}-dead-letters", Ct))["QueueArn"];
            foreach (var queue in new[] { $"{serviceName}-commands", $"{serviceName}-requests", $"{serviceName}-events-{instanceId}", $"{serviceName}-responses-{instanceId}" })
            {
                using var redrivePolicy = JsonDocument.Parse((await TestBroker.QueueAttributesAsync(queue, Ct))["RedrivePolicy"]);
                Assert.Equal(deadLetterArn, redrivePolicy.RootElement.GetProperty("deadLetterTargetArn").GetString());
                Assert.Equal(3, int.Parse(redrivePolicy.RootElement.GetProperty("maxReceiveCount").ToString()));
            }
        }

        private static async Task<Message> ReceiveDeadLetterAsync(string serviceName)
        {
            using var sqs = TestBroker.CreateSqsClient();
            var deadLetters = await TestBroker.QueueUrlAsync($"{serviceName}-dead-letters", Ct);
            var deadline = DateTime.UtcNow + Timeout;
            while (DateTime.UtcNow < deadline)
            {
                var response = await sqs.ReceiveMessageAsync(new ReceiveMessageRequest
                {
                    QueueUrl = deadLetters,
                    WaitTimeSeconds = 1,
                    MessageAttributeNames = new List<string> { "All" },
                }, Ct);
                if (response.Messages?.Count > 0)
                {
                    return response.Messages[0];
                }
            }
            throw new TimeoutException($"Nothing reached {serviceName}-dead-letters in time.");
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
    }
}
