using System;
using System.Threading;
using NUnit.Framework;

namespace Obvs.Tests
{
    [TestFixture]
    public class TestSubjectMessageBus
    {
        SubjectMessageBus<string> _testObj;

        [SetUp]
        public void Setup()
        {
            _testObj = new SubjectMessageBus<string>();
        }

        [TearDown]
        public void TearDown()
        {

        }

        [Test]
        public void PublishBlocksUntilMessageHasBeenConsumed()
        {
            int subscriberThreadId = -1;
            _testObj.Messages.Subscribe(_ => subscriberThreadId = Thread.CurrentThread.ManagedThreadId);

            int threadId = Thread.CurrentThread.ManagedThreadId;

            var task = _testObj.PublishAsync("Bleb");

            Assert.AreNotEqual(-1, subscriberThreadId);
            Assert.AreEqual(threadId, subscriberThreadId);

            task.Wait();
        }


        [Test]
        public void PublishReturnsWithNoObservers()
        {
            Assert.IsTrue(_testObj.PublishAsync("Bleb").Wait(1));
        }
    }
}