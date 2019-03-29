using System;
using System.Collections.Generic;
using App.Metrics;
using App.Metrics.Meter;
using FakeItEasy;
using Obvs.Monitoring.AppMetrics;
using Xunit;

namespace Obvs.Monitoring.Tests
{
    public class TestAppMetricsMonitoring
    {
        [Fact]
        public void ShouldBeAbleToCreateMonitor()
        {
            IMetrics metrics = A.Fake<IMetrics>();
            IMonitorFactory<TestMessage> factory = new AppMetricsMonitorFactory<TestMessage>(metrics, new List<Type>(), "instancePrefix");

            IMonitor<TestMessage> monitor = factory.Create("SomeName");

            Assert.NotNull(monitor);
        }

        [Fact]
        public void ShouldAttemptToSaveIfMessagesSent()
        {
            IMetrics metrics = A.Fake<IMetrics>();
            IMonitorFactory<TestMessage> factory = new AppMetricsMonitorFactory<TestMessage>(metrics, new List<Type>(), "instancePrefix");

            IMonitor<TestMessage> monitor = factory.Create("SomeName");

            monitor.MessageSent(new TestMessage(), TimeSpan.FromMilliseconds(1));

            A.CallTo(() => metrics.Measure.Meter.Mark(A<MeterOptions>._, A<MetricTags>._)).MustHaveHappenedOnceExactly();
        }

    }
}
