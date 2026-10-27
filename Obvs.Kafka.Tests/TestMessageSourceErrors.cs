using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Logging;
using Obvs.Kafka.Configuration;
using Obvs.Serialization;
using Obvs.Serialization.Json;
using Xunit;
using IMessage = Obvs.Types.IMessage;

namespace Obvs.Kafka.Tests
{
    // Which errors the Kafka client reports end a subscription. The client recovers from most of them by itself.
    public class TestMessageSourceErrors
    {
        public interface ITestMessage : IMessage
        {
        }

        public class TestMessage : ITestMessage
        {
            public string Data { get; set; }
        }

        public static IEnumerable<object[]> ErrorsTheClientRecoversFrom => new[]
        {
            new object[] { new Error(ErrorCode.Local_AllBrokersDown, "1/1 brokers are down") },
            new object[] { new Error(ErrorCode.Local_Transport, "kafka.example:9093/0: Disconnected (after 240000ms in state UP)") },
            new object[] { new Error(ErrorCode.Local_Resolve, "Failed to resolve 'kafka.example:9093'") },
            new object[] { new Error(ErrorCode.Local_TimedOut, "Timed out") },
            // An OAUTHBEARER token refresh that failed: librdkafka keeps the token it has and tries again in 10 seconds
            new object[] { new Error(ErrorCode.Local_Authentication, "Failed to acquire SASL OAUTHBEARER token: token endpoint unavailable") },
            new object[] { new Error(ErrorCode.Local_ValueDeserialization, "Invalid wire type") },
            new object[] { new Error(ErrorCode.UnknownTopicOrPart, "Subscribed topic not available: orders.Commands") },
        };

        public static IEnumerable<object[]> ErrorsThatEndTheSubscription => new[]
        {
            new object[] { new Error(ErrorCode.Local_Fatal, "Fatal error: fenced by another instance", true) },
            new object[] { new Error(ErrorCode.FencedInstanceId, "Fenced by another static member", true) },
            new object[] { new Error(ErrorCode.TopicAuthorizationFailed, "Not authorized to access topics: [orders.Commands]") },
            new object[] { new Error(ErrorCode.GroupAuthorizationFailed, "Not authorized to access group: orders.Commands") },
            new object[] { new Error(ErrorCode.ClusterAuthorizationFailed, "Cluster authorization failed") },
        };

        [Theory]
        [MemberData(nameof(ErrorsTheClientRecoversFrom))]
        public void ShouldLogAndCarryOnAfterAnErrorTheClientRecoversFrom(Error error)
        {
            var logger = new RecordingLogger();
            var source = Source(logger);
            var observer = new RecordingObserver();

            var ends = source.OnClientError(error, observer);

            Assert.False(ends);
            Assert.Null(observer.Error);
            var entry = Assert.Single(logger.Entries);
            Assert.Equal(LogLevel.Warning, entry.Level);
            Assert.Contains(error.Reason, entry.Message);
            Assert.Contains("orders.Commands", entry.Message);
        }

        [Theory]
        [MemberData(nameof(ErrorsThatEndTheSubscription))]
        public void ShouldEndTheSubscriptionOnAnErrorThatWontFixItself(Error error)
        {
            var source = Source(new RecordingLogger());
            var observer = new RecordingObserver();

            var ends = source.OnClientError(error, observer);

            Assert.True(ends);
            var exception = Assert.IsType<KafkaException>(observer.Error);
            Assert.Equal(error.Code, exception.Error.Code);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldSkipAMessageItCantReadAndCarryOn()
        {
            var topic = $"obvs-kafka-unreadable-{Guid.NewGuid():N}";
            using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = TestBroker.BootstrapServers }).Build();
            await admin.CreateTopicsAsync(new[] { new TopicSpecification { Name = topic, NumPartitions = 1, ReplicationFactor = 1 } });
            try
            {
                var configuration = new KafkaConfiguration(TestBroker.BootstrapServers);
                var source = new MessageSource<ITestMessage>(configuration, new KafkaSourceConfiguration(), topic,
                    new IMessageDeserializer<ITestMessage>[] { new JsonMessageDeserializer<TestMessage>() }, null);
                var publisher = new MessagePublisher<ITestMessage>(configuration, new KafkaProducerConfiguration(), topic, new JsonMessageSerializer(), null);
                var received = new ConcurrentQueue<TestMessage>();
                Exception error = null;

                using (source.Messages.OfType<TestMessage>().Subscribe(received.Enqueue, exception => error = exception))
                using (var producer = new ProducerBuilder<Null, byte[]>(new ProducerConfig { BootstrapServers = TestBroker.BootstrapServers }).Build())
                {
                    // Not an Obvs message, so the consumer can't deserialize it
                    await producer.ProduceAsync(topic, new Message<Null, byte[]> { Value = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF } }, TestContext.Current.CancellationToken);
                    await publisher.PublishAsync(new TestMessage { Data = "after the unreadable one" }, TestContext.Current.CancellationToken);

                    var deadline = DateTime.UtcNow.AddSeconds(10);
                    while (received.IsEmpty && error == null && DateTime.UtcNow < deadline)
                    {
                        await Task.Delay(100, TestContext.Current.CancellationToken);
                    }
                }

                Assert.Null(error);
                Assert.Equal("after the unreadable one", Assert.Single(received).Data);
            }
            finally
            {
                await admin.DeleteTopicsAsync(new[] { topic });
            }
        }

        private static MessageSource<ITestMessage> Source(ILogger logger)
        {
            var configuration = new KafkaConfiguration("kafka.example:9093") { Logger = logger };
            return new MessageSource<ITestMessage>(configuration, new KafkaSourceConfiguration(), "orders.Commands", new IMessageDeserializer<ITestMessage>[0], null);
        }

        private sealed class RecordingObserver : IObserver<ITestMessage>
        {
            public Exception Error { get; private set; }

            public void OnCompleted()
            {
            }

            public void OnError(Exception error) => Error = error;

            public void OnNext(ITestMessage value)
            {
            }
        }

        private sealed class RecordingLogger : ILogger
        {
            public List<(LogLevel Level, string Message)> Entries { get; } = new List<(LogLevel, string)>();

            public IDisposable BeginScope<TState>(TState state) => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                Entries.Add((logLevel, formatter(state, exception)));
            }
        }
    }
}
