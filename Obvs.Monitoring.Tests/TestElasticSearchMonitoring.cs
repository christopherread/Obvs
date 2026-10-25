using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using Microsoft.Reactive.Testing;
using Obvs.Monitoring.ElasticSearch;
using Xunit;

namespace Obvs.Monitoring.Tests
{
    public class TestElasticSearchMonitoring
    {
        private readonly List<ApiCallDetails> _calls = new List<ApiCallDetails>();
        private readonly ElasticsearchClient _elasticClient;

        public TestElasticSearchMonitoring()
        {
            // Send requests to an in-memory transport that answers like a real server, and record them
            var invoker = new InMemoryRequestInvoker(
                Encoding.UTF8.GetBytes("{\"took\":1,\"errors\":false,\"items\":[]}"), 200, null, "application/json",
                new Dictionary<string, IEnumerable<string>> { ["x-elastic-product"] = new[] { "Elasticsearch" } });
            var settings = new ElasticsearchClientSettings(invoker)
                .DisableDirectStreaming()
                .OnRequestCompleted(_calls.Add);
            _elasticClient = new ElasticsearchClient(settings);
        }

        private IEnumerable<ApiCallDetails> BulkRequests()
        {
            return _calls.Where(call => call.Uri.AbsolutePath.EndsWith("/_bulk"));
        }

        // A bulk request body has an action line then a document line for each indexed counter
        private static int IndexOperations(ApiCallDetails call)
        {
            return Encoding.UTF8.GetString(call.RequestBodyInBytes)
                .Split('\n')
                .Count(line => line.StartsWith("{\"index\""));
        }

        [Fact]
        public void ShouldBeAbleToCreateMonitor()
        {
            TestScheduler testScheduler = new TestScheduler();

            IMonitorFactory<TestMessage> factory = new ElasticSearchMonitorFactory<TestMessage>("indexName", new List<Type>(), "instanceName", TimeSpan.FromSeconds(1), testScheduler, _elasticClient);

            IMonitor<TestMessage> monitor = factory.Create("SomeName");

            Assert.NotNull(monitor);
        }

        [Fact]
        public void ShouldNotAttemptToSaveIfNoMessages()
        {
            TestScheduler testScheduler = new TestScheduler();

            IMonitorFactory<TestMessage> factory = new ElasticSearchMonitorFactory<TestMessage>("indexName", new List<Type>(), "instanceName", TimeSpan.FromSeconds(1), testScheduler, _elasticClient);

            IMonitor<TestMessage> monitor = factory.Create("SomeName");

            Assert.NotNull(monitor);

            testScheduler.AdvanceBy(TimeSpan.FromMinutes(1).Ticks);

            Assert.Empty(BulkRequests());
        }

        [Fact]
        public void ShouldAttemptToSaveIfMessagesSent()
        {
            TestScheduler testScheduler = new TestScheduler();

            IMonitorFactory<TestMessage> factory = new ElasticSearchMonitorFactory<TestMessage>("indexName", new List<Type>(), "instanceName", TimeSpan.FromSeconds(1), testScheduler, _elasticClient);

            IMonitor<TestMessage> monitor = factory.Create("SomeName");

            monitor.MessageSent(new TestMessage(), TimeSpan.FromMilliseconds(1));

            testScheduler.AdvanceBy(TimeSpan.FromMinutes(1).Ticks);

            Assert.Single(BulkRequests());
        }

        [Fact]
        public void ShouldAttemptToSaveIfMessagesReceived()
        {
            TestScheduler testScheduler = new TestScheduler();

            IMonitorFactory<TestMessage> factory = new ElasticSearchMonitorFactory<TestMessage>("indexName", new List<Type>(), "instanceName", TimeSpan.FromSeconds(1), testScheduler, _elasticClient);

            IMonitor<TestMessage> monitor = factory.Create("SomeName");

            monitor.MessageReceived(new TestMessage(), TimeSpan.FromMilliseconds(1));

            testScheduler.AdvanceBy(TimeSpan.FromMinutes(1).Ticks);

            Assert.Single(BulkRequests());
        }


        [Fact]
        public void ShouldAttemptToSaveMultipleCountersIfMessagesSent()
        {
            TestScheduler testScheduler = new TestScheduler();

            var types = new List<Type> { typeof(TestMessage) };
            IMonitorFactory<TestMessage> factory = new ElasticSearchMonitorFactory<TestMessage>("indexName", types, "instanceName", TimeSpan.FromSeconds(1), testScheduler, _elasticClient);

            IMonitor<TestMessage> monitor = factory.Create("SomeName");

            monitor.MessageSent(new TestMessage(), TimeSpan.FromMilliseconds(1));

            testScheduler.AdvanceBy(TimeSpan.FromMinutes(1).Ticks);

            var bulk = Assert.Single(BulkRequests());
            Assert.Equal(1 + types.Count, IndexOperations(bulk));
        }
        
        [Fact]
        public void ShouldAttemptToSaveMultipleCountersIfMessagesReceived()
        {
            TestScheduler testScheduler = new TestScheduler();

            var types = new List<Type> {typeof(TestMessage)};
            IMonitorFactory<TestMessage> factory = new ElasticSearchMonitorFactory<TestMessage>("indexName", types, "instanceName", TimeSpan.FromSeconds(1), testScheduler, _elasticClient);

            IMonitor<TestMessage> monitor = factory.Create("SomeName");

            monitor.MessageReceived(new TestMessage(), TimeSpan.FromMilliseconds(1));

            testScheduler.AdvanceBy(TimeSpan.FromMinutes(1).Ticks);

            var bulk = Assert.Single(BulkRequests());
            Assert.Equal(1 + types.Count, IndexOperations(bulk));
        }
    
        [Fact]
        public void ShouldIndexCountersInDailyIndexWithCamelCaseFields()
        {
            TestScheduler testScheduler = new TestScheduler();

            IMonitorFactory<TestMessage> factory = new ElasticSearchMonitorFactory<TestMessage>("indexName", new List<Type>(), "instanceName", TimeSpan.FromSeconds(1), testScheduler, _elasticClient);

            IMonitor<TestMessage> monitor = factory.Create("SomeName");

            monitor.MessageSent(new TestMessage(), TimeSpan.FromMilliseconds(1));

            testScheduler.AdvanceBy(TimeSpan.FromMinutes(1).Ticks);

            // Same index naming and document shape as the NEST-based versions, so existing indexes keep working
            var bulk = Assert.Single(BulkRequests());
            Assert.Equal($"/indexName-{DateTime.Today:yyyy.MM.dd}/_bulk", bulk.Uri.AbsolutePath);
            Assert.True(bulk.HasSuccessfulStatusCode);
            var body = Encoding.UTF8.GetString(bulk.RequestBodyInBytes);
            Assert.Contains("\"counterName\":\"SomeName\"", body);
            Assert.Contains("\"instanceName\":\"instanceName\"", body);
            Assert.Contains("\"direction\":\"send\"", body);
        }

        [Fact]
        public void ShouldDisposeCleanly()
        {
            TestScheduler testScheduler = new TestScheduler();

            IMonitorFactory<TestMessage> factory = new ElasticSearchMonitorFactory<TestMessage>("indexName", new List<Type>(), "instanceName", TimeSpan.FromSeconds(1), testScheduler, _elasticClient);

            IMonitor<TestMessage> monitor = factory.Create("SomeName");

            monitor.Dispose();
        }
    }
}
