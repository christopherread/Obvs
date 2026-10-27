using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Obvs.AzureSB.Tests.Hosting;
using Obvs.Serialization;
using Obvs.Serialization.Json;
using Obvs.Types;
using Xunit;

namespace Obvs.AzureSB.Tests
{
    // What a message source does with the errors its processor reports. Service Bus is stood in for: the Explicit
    // tests in Hosting cover a real one.
    public class TestMessageSourceErrors
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        [Fact]
        public async Task ShouldLogErrorsTheProcessorRetries()
        {
            var logger = new RecordingLogger();
            var processor = new StandInProcessor();
            var source = Source(processor, logger);
            using var subscription = source.Messages.Subscribe(_ => { }, _ => { });

            await processor.RaiseErrorAsync(new ServiceBusException("The connection was lost", ServiceBusFailureReason.ServiceCommunicationProblem));

            var entry = Assert.Single(logger.Entries);
            Assert.Equal(LogLevel.Warning, entry.Level);
            Assert.Contains("orders.Commands", entry.Message);
            Assert.Contains("The connection was lost", entry.Message);
        }

        [Fact]
        public async Task ShouldReportErrorsTheProcessorWontRetryRatherThanLogThem()
        {
            // The bus logs what's reported on its Exceptions stream
            var logger = new RecordingLogger();
            var processor = new StandInProcessor();
            var source = Source(processor, logger);
            var reported = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var subscription = source.Messages.Subscribe(_ => { }, error => reported.TrySetResult(error));

            await processor.RaiseErrorAsync(new ServiceBusException("The queue was deleted", ServiceBusFailureReason.MessagingEntityNotFound));

            Assert.Contains("The queue was deleted", (await reported.Task.WaitAsync(TimeSpan.FromSeconds(5), Ct)).Message);
            Assert.Empty(logger.Entries);
        }

        [Fact]
        public async Task ShouldCountAMessageItCantDeserialize()
        {
            var counted = new ConcurrentQueue<(string Type, object ErrorType)>();
            var serviceName = "orders-" + Guid.NewGuid().ToString("N");
            using var listener = ConsumedMessagesListener(serviceName, counted);
            var processor = new StandInProcessor();
            var source = new MessageSource<ICommand>(_ => processor, new IMessageDeserializer<ICommand>[] { new JsonMessageDeserializer<PlaceOrder>() },
                new AzureServiceBusOptions { ServiceName = serviceName });
            using var subscription = source.Messages.Subscribe(_ => { }, _ => { });
            var corrupt = ServiceBusModelFactory.ServiceBusReceivedMessage(BinaryData.FromString("not json"),
                properties: new Dictionary<string, object> { [Obvs.MessageProperties.MessagePropertyNames.TypeName] = nameof(PlaceOrder) });

            await processor.RaiseMessageAsync(corrupt);

            var (type, errorType) = Assert.Single(counted);
            Assert.Equal(nameof(PlaceOrder), type);
            Assert.NotNull(errorType);
        }

        private static System.Diagnostics.Metrics.MeterListener ConsumedMessagesListener(string serviceName, ConcurrentQueue<(string Type, object ErrorType)> counted)
        {
            var listener = new System.Diagnostics.Metrics.MeterListener
            {
                InstrumentPublished = (instrument, l) =>
                {
                    if (instrument.Meter.Name == Obvs.Hosting.ObvsTelemetry.MeterName && instrument.Name == "messaging.client.consumed.messages")
                    {
                        l.EnableMeasurementEvents(instrument);
                    }
                },
            };
            listener.SetMeasurementEventCallback<long>((instrument, value, tags, state) =>
            {
                var tagged = new Dictionary<string, object>();
                foreach (var tag in tags)
                {
                    tagged[tag.Key] = tag.Value;
                }
                if ((string)tagged["messaging.destination.name"] == serviceName)
                {
                    counted.Enqueue(((string)tagged["obvs.message.type"], tagged.TryGetValue("error.type", out var errorType) ? errorType : null));
                }
            });
            listener.Start();
            return listener;
        }

        private static MessageSource<ICommand> Source(StandInProcessor processor, ILogger logger)
        {
            var options = new AzureServiceBusOptions { Logger = logger };
            return new MessageSource<ICommand>(_ => processor, new IMessageDeserializer<ICommand>[] { new JsonMessageDeserializer<PlaceOrder>() }, options);
        }

        private sealed class StandInProcessor : ServiceBusProcessor
        {
            public override string EntityPath => "orders.Commands";

            public Task RaiseErrorAsync(Exception exception)
            {
                return OnProcessErrorAsync(new ProcessErrorEventArgs(exception, ServiceBusErrorSource.Receive, "contoso.servicebus.windows.net", EntityPath, "processor-1", Ct));
            }

            public Task RaiseMessageAsync(ServiceBusReceivedMessage message)
            {
                return OnProcessMessageAsync(new ProcessMessageEventArgs(message, FakeItEasy.A.Fake<ServiceBusReceiver>(), Ct));
            }

            public override Task StartProcessingAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

            public override Task StopProcessingAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

            public override Task CloseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        }

        private sealed class RecordingLogger : ILogger
        {
            public ConcurrentQueue<(LogLevel Level, string Message)> Entries { get; } = new ConcurrentQueue<(LogLevel, string)>();

            public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                Entries.Enqueue((logLevel, formatter(state, exception)));
            }
        }
    }
}
