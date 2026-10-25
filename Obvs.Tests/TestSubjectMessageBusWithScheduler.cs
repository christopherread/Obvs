using System;
using Microsoft.Reactive.Testing;
using System.Threading.Tasks;
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
        public async Task OnNextReturnsImmediatelyEvenIfMessageIsntEmitted()
        {
            bool received = false;
            _testObj.Messages.Subscribe(_ => received = true);

            await _testObj.PublishAsync("Bleb");

            Assert.False(received);
        }

        [Fact]
        public async Task OnNextReturnsImmediatelyThenMessageEmitted()
        {
            bool received = false;
            _testObj.Messages.Subscribe(_ => received = true);

            await _testObj.PublishAsync("Bleb");

            _scheduler.AdvanceBy(1);

            Assert.True(received);
        }

        [Fact]
        public async Task PublishReturnsImmediatelyWithNoObservers()
        {
            await _testObj.PublishAsync("Bleb");
        }

        [Fact]
        public async Task PublishReturnsImmediatelyWithNoObservers_NoCachedMessages()
        {
            await _testObj.PublishAsync("Bleb");

            bool received = false;
            _testObj.Messages.Subscribe(_ => received = true);

            _scheduler.AdvanceBy(10);

            Assert.False(received);
        }

    }
}