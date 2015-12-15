using System;
using Microsoft.Reactive.Testing;
using NUnit.Framework;

namespace Obvs.Tests
{
    [TestFixture]
    public class TestScheduledSubjectMessageBus
    {
        TestScheduler _scheduler = new TestScheduler();
        ScheduledSubjectMessageBus<string> _testObj;

        [SetUp]
        public void Setup()
        {
            _scheduler = new TestScheduler();
            _testObj = new ScheduledSubjectMessageBus<string>(_scheduler);
        }

        [TearDown]
        public void TearDown()
        {
        }

        [Test]
        public void OnNextReturnsImmediatelyEvenIfMessageIsntEmitted()
        {
            bool received = false;
            _testObj.Messages.Subscribe(_ => received = true);

            _testObj.PublishAsync("Bleb").Wait();

            Assert.IsFalse(received);
        }

        [Test]
        public void OnNextReturnsImmediatelyThenMessageEmitted()
        {
            bool received = false;
            _testObj.Messages.Subscribe(_ => received = true);

            _testObj.PublishAsync("Bleb").Wait();

            _scheduler.AdvanceBy(1);

            Assert.IsTrue(received);
        }

        [Test]
        public void PublishReturnsImmediatelyWithNoObservers()
        {
            _testObj.PublishAsync("Bleb").Wait();
        }

        [Test]
        public void PublishReturnsImmediatelyWithNoObservers_NoCachedMessages()
        {
            _testObj.PublishAsync("Bleb").Wait();

            bool received = false;
            _testObj.Messages.Subscribe(_ => received = true);

            _scheduler.AdvanceBy(10);

            Assert.IsFalse(received);
        }

    }
}