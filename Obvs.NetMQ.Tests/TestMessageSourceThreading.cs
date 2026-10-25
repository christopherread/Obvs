using System;
using System.Threading;
using System.Threading.Tasks;
using Obvs.Serialization;
using Obvs.Serialization.Json;
using Obvs.Types;
using Xunit;

namespace Obvs.NetMQ.Tests
{
    [CollectionDefinition(nameof(ThreadPoolSaturation), DisableParallelization = true)]
    public class ThreadPoolSaturation
    {
    }

    // Each subscription runs a receive loop that blocks for as long as it's subscribed. If those loops
    // need thread-pool threads, a busy pool (as on CI's small runners) delays subscribers long enough
    // for publishers to give up waiting for them, and the first messages are dropped.
    [Collection(nameof(ThreadPoolSaturation))]
    public class TestMessageSourceThreading
    {
        [Fact]
        public void SubscribingDoesNotWaitForAThreadPoolThread()
        {
            var release = new ManualResetEventSlim(false);
            try
            {
                // Far more blocked work than the pool has threads, so anything queued after it waits
                for (var i = 0; i < 500; i++)
                {
                    ThreadPool.QueueUserWorkItem(_ => release.Wait());
                }

                var source = new MessageSource<IMessage>("tcp://localhost:5580",
                    new IMessageDeserializer<IMessage>[] { new JsonMessageDeserializer<ThreadingMessage>() }, "ThreadingTopic");

                // Subscribe blocks until the receive loop starts, so call it from a thread of its own
                var subscribe = Task.Factory.StartNew(() => source.Messages.Subscribe(_ => { }), TaskCreationOptions.LongRunning);

                // Blocking on purpose: with the pool saturated, an await might not get a thread to resume on
#pragma warning disable xUnit1031
                Assert.True(subscribe.Wait(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken),
                    "Subscribing waited for a thread-pool thread to start receiving");
                subscribe.Result.Dispose();
#pragma warning restore xUnit1031
            }
            finally
            {
                release.Set();
            }
        }

        public class ThreadingMessage : IMessage
        {
        }
    }
}
