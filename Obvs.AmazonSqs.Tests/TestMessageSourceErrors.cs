using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SQS;
using Amazon.SQS.Model;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using Obvs.AmazonSqs.Tests.Hosting;
using Obvs.Serialization;
using Obvs.Serialization.Json;
using Obvs.Types;
using Xunit;

namespace Obvs.AmazonSqs.Tests
{
    // What a message source does with errors receiving. SQS is faked: the Explicit tests in Hosting cover a real one.
    public class TestMessageSourceErrors
    {
        private const string QueueUrl = "http://localhost:4566/000000000000/orders-Commands";

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        [Fact]
        public async Task ShouldLogOnceWhileRetryingAndAgainOnceItReceives()
        {
            var logger = new RecordingLogger();
            var sqs = A.Fake<IAmazonSQS>();
            var receives = 0;
            A.CallTo(() => sqs.ReceiveMessageAsync(A<ReceiveMessageRequest>._, A<CancellationToken>._))
                .ReturnsLazily(() => Interlocked.Increment(ref receives) <= 2
                    ? Task.FromException<ReceiveMessageResponse>(new HttpRequestException("No route to host"))
                    : Task.FromResult(new ReceiveMessageResponse { Messages = new List<Message>() }));
            var source = new MessageSource<ICommand>(sqs, QueueUrl, null, new IMessageDeserializer<ICommand>[] { new JsonMessageDeserializer<PlaceOrder>() },
                new AmazonSqsOptions { Logger = logger });
            var errors = new ConcurrentQueue<Exception>();

            using (source.Messages.Subscribe(_ => { }, errors.Enqueue))
            {
                await WaitUntilAsync(() => logger.Entries.Count == 2);
            }

            Assert.Collection(logger.Entries,
                entry =>
                {
                    Assert.Equal(LogLevel.Warning, entry.Level);
                    Assert.Contains(QueueUrl, entry.Message);
                    Assert.Contains("No route to host", entry.Message);
                },
                entry => Assert.Equal(LogLevel.Information, entry.Level));
            Assert.Empty(errors);
        }

        private static async Task WaitUntilAsync(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
            while (!condition())
            {
                if (DateTime.UtcNow > deadline)
                {
                    throw new TimeoutException("The condition wasn't met in time.");
                }
                await Task.Delay(20, Ct);
            }
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
