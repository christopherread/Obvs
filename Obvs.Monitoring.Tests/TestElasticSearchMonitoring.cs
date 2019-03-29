using System;
using System.Collections.Generic;
using FakeItEasy;
using Microsoft.Reactive.Testing;
using Nest;
using Obvs.Monitoring.ElasticSearch;
using Xunit;

namespace Obvs.Monitoring.Tests
{
    public class TestElasticSearchMonitoring
    {
        [Fact]
        public void ShouldBeAbleToCreateMonitor()
        {
            IElasticClient elasticClient = A.Fake<IElasticClient>();
            TestScheduler testScheduler = new TestScheduler();

            IMonitorFactory<TestMessage> factory = new ElasticSearchMonitorFactory<TestMessage>("indexName", new List<Type>(), "instanceName", TimeSpan.FromSeconds(1), testScheduler, elasticClient);

            IMonitor<TestMessage> monitor = factory.Create("SomeName");

            Assert.NotNull(monitor);
        }

        [Fact]
        public void ShouldNotAttemptToSaveIfNoMessages()
        {
            IElasticClient elasticClient = A.Fake<IElasticClient>();
            TestScheduler testScheduler = new TestScheduler();

            IMonitorFactory<TestMessage> factory = new ElasticSearchMonitorFactory<TestMessage>("indexName", new List<Type>(), "instanceName", TimeSpan.FromSeconds(1), testScheduler, elasticClient);

            IMonitor<TestMessage> monitor = factory.Create("SomeName");

            Assert.NotNull(monitor);

            testScheduler.AdvanceBy(TimeSpan.FromMinutes(1).Ticks);

            A.CallTo(() => elasticClient.Bulk(A<IBulkRequest>._)).MustNotHaveHappened();
        }

        [Fact]
        public void ShouldAttemptToSaveIfMessagesSent()
        {
            IElasticClient elasticClient = A.Fake<IElasticClient>();
            TestScheduler testScheduler = new TestScheduler();

            IMonitorFactory<TestMessage> factory = new ElasticSearchMonitorFactory<TestMessage>("indexName", new List<Type>(), "instanceName", TimeSpan.FromSeconds(1), testScheduler, elasticClient);

            IMonitor<TestMessage> monitor = factory.Create("SomeName");

            monitor.MessageSent(new TestMessage(), TimeSpan.FromMilliseconds(1));

            testScheduler.AdvanceBy(TimeSpan.FromMinutes(1).Ticks);

            A.CallTo(() => elasticClient.Bulk(A<IBulkRequest>._)).MustHaveHappened(Repeated.Exactly.Once);
        }

        [Fact]
        public void ShouldAttemptToSaveIfMessagesReceived()
        {
            IElasticClient elasticClient = A.Fake<IElasticClient>();
            TestScheduler testScheduler = new TestScheduler();

            IMonitorFactory<TestMessage> factory = new ElasticSearchMonitorFactory<TestMessage>("indexName", new List<Type>(), "instanceName", TimeSpan.FromSeconds(1), testScheduler, elasticClient);

            IMonitor<TestMessage> monitor = factory.Create("SomeName");

            monitor.MessageReceived(new TestMessage(), TimeSpan.FromMilliseconds(1));

            testScheduler.AdvanceBy(TimeSpan.FromMinutes(1).Ticks);

            A.CallTo(() => elasticClient.Bulk(A<IBulkRequest>._)).MustHaveHappened(Repeated.Exactly.Once);
        }


        [Fact]
        public void ShouldAttemptToSaveMultipleCountersIfMessagesSent()
        {
            IElasticClient elasticClient = A.Fake<IElasticClient>();
            TestScheduler testScheduler = new TestScheduler();

            var types = new List<Type> { typeof(TestMessage) };
            IMonitorFactory<TestMessage> factory = new ElasticSearchMonitorFactory<TestMessage>("indexName", types, "instanceName", TimeSpan.FromSeconds(1), testScheduler, elasticClient);

            IMonitor<TestMessage> monitor = factory.Create("SomeName");

            monitor.MessageSent(new TestMessage(), TimeSpan.FromMilliseconds(1));

            testScheduler.AdvanceBy(TimeSpan.FromMinutes(1).Ticks);

            A.CallTo(() => elasticClient.Bulk(A<IBulkRequest>.That.Matches(request => request.Operations.Count == 1 + types.Count))).MustHaveHappened(Repeated.Exactly.Once);
        }
        
        [Fact]
        public void ShouldAttemptToSaveMultipleCountersIfMessagesReceived()
        {
            IElasticClient elasticClient = A.Fake<IElasticClient>();
            TestScheduler testScheduler = new TestScheduler();

            var types = new List<Type> {typeof(TestMessage)};
            IMonitorFactory<TestMessage> factory = new ElasticSearchMonitorFactory<TestMessage>("indexName", types, "instanceName", TimeSpan.FromSeconds(1), testScheduler, elasticClient);

            IMonitor<TestMessage> monitor = factory.Create("SomeName");

            monitor.MessageReceived(new TestMessage(), TimeSpan.FromMilliseconds(1));

            testScheduler.AdvanceBy(TimeSpan.FromMinutes(1).Ticks);

            A.CallTo(() => elasticClient.Bulk(A<IBulkRequest>.That.Matches(request => request.Operations.Count == 1 + types.Count))).MustHaveHappened(Repeated.Exactly.Once);
        }
    
        [Fact]
        public void ShouldDisposeCleanly()
        {
            IElasticClient elasticClient = A.Fake<IElasticClient>();
            TestScheduler testScheduler = new TestScheduler();

            IMonitorFactory<TestMessage> factory = new ElasticSearchMonitorFactory<TestMessage>("indexName", new List<Type>(), "instanceName", TimeSpan.FromSeconds(1), testScheduler, elasticClient);

            IMonitor<TestMessage> monitor = factory.Create("SomeName");

            monitor.Dispose();
        }
    }
}
