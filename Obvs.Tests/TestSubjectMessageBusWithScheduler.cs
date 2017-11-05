using System;
using Microsoft.Reactive.Testing;
using Xunit;

namespace Obvs.Tests
{
    
    public class TestSubjectMessageBusWithScheduler
    {
        readonly TestScheduler _scheduler;
        readonly SubjectMessageBus<string> _testObj;

        public TestSubjectMessageBusWithScheduler()
        {
            _scheduler = new TestScheduler();
            _testObj = new SubjectMessageBus<string>(_scheduler);
        }

        [Fact]
        public void OnNextReturnsImmediatelyEvenIfMessageIsntEmitted()
        {
            bool received = false;
            _testObj.Messages.Subscribe(_ => received = true);

            _testObj.PublishAsync("Bleb").Wait();

            Assert.False(received);
        }

        [Fact]
        public void OnNextReturnsImmediatelyThenMessageEmitted()
        {
            bool received = false;
            _testObj.Messages.Subscribe(_ => received = true);

            _testObj.PublishAsync("Bleb").Wait();

            _scheduler.AdvanceBy(1);

            Assert.True(received);
        }

        [Fact]
        public void PublishReturnsImmediatelyWithNoObservers()
        {
            _testObj.PublishAsync("Bleb").Wait();
        }

        [Fact]
        public void PublishReturnsImmediatelyWithNoObservers_NoCachedMessages()
        {
            _testObj.PublishAsync("Bleb").Wait();

            bool received = false;
            _testObj.Messages.Subscribe(_ => received = true);

            _scheduler.AdvanceBy(10);

            Assert.False(received);
        }

    }
}