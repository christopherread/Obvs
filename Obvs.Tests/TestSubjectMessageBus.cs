using System;
using System.Threading;
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
        public void PublishBlocksUntilMessageHasBeenConsumed()
        {
            int subscriberThreadId = -1;
            _testObj.Messages.Subscribe(_ => subscriberThreadId = Thread.CurrentThread.ManagedThreadId);

            int threadId = Thread.CurrentThread.ManagedThreadId;

            var task = _testObj.PublishAsync("Bleb");

            Assert.NotEqual(-1, subscriberThreadId);
            Assert.Equal(threadId, subscriberThreadId);

            task.Wait();
        }


        [Fact]
        public void PublishReturnsWithNoObservers()
        {
            Assert.True(_testObj.PublishAsync("Bleb").Wait(1));
        }
    }
}