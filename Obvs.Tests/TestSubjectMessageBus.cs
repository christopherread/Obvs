using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Obvs.Tests
{
    
    public class TestSubjectMessageBus
    {
        readonly SubjectMessageBus<string> _testObj;

        public TestSubjectMessageBus()
        {
            _testObj = new SubjectMessageBus<string>();
        }

        [Fact]
        public async Task PublishBlocksUntilMessageHasBeenConsumed()
        {
            int subscriberThreadId = -1;
            _testObj.Messages.Subscribe(_ => subscriberThreadId = Thread.CurrentThread.ManagedThreadId);

            int threadId = Thread.CurrentThread.ManagedThreadId;

            var task = _testObj.PublishAsync("Bleb");

            Assert.NotEqual(-1, subscriberThreadId);
            Assert.Equal(threadId, subscriberThreadId);

            await task;
        }


        [Fact]
        public void PublishReturnsWithNoObservers()
        {
            Assert.True(_testObj.PublishAsync("Bleb").IsCompleted);
        }
    }
}