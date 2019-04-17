using System;
using System.Collections.Generic;
using App.Metrics;
using App.Metrics.Meter;
using App.Metrics.Timer;
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
            ITimer fakeTimer = A.Fake<ITimer>();
            A.CallTo(() => metrics.Provider.Timer.Instance(A<TimerOptions>._, A<MetricTags>._)).Returns(fakeTimer);

            IMonitorFactory<TestMessage> factory = new AppMetricsMonitorFactory<TestMessage>(metrics, new List<Type>(), "instancePrefix");

            IMonitor<TestMessage> monitor = factory.Create("SomeName");

            monitor.MessageSent(new TestMessage(), TimeSpan.FromMilliseconds(1));

            A.CallTo(() => metrics.Provider.Timer.Instance(A<TimerOptions>._, A<MetricTags>._)).MustHaveHappenedOnceExactly();
            A.CallTo(() => fakeTimer.Record(A<long>._, A<TimeUnit>._)).MustHaveHappenedOnceExactly();

        }

    }
}
