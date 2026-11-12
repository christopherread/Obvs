using System.Threading.Tasks;
using Xunit;

namespace Obvs.ActiveMQ.Tests
{
    // v6 shipped copies of a pooled memory stream and a task scheduler; v7 uses .NET's own, so apps don't take them from a transport
    public class TestPublicApi
    {
        private static readonly System.Reflection.Assembly ActiveMq = typeof(ActiveMqOptions).Assembly;

        [Fact]
        public void ShouldNotShipACopyOfRecyclableMemoryStream()
        {
            Assert.DoesNotContain(ActiveMq.GetTypes(), type => type.Name.StartsWith("RecyclableMemoryStream") || type.Namespace == "Obvs.ActiveMQ.RecyclableMemoryStream");
        }

        [Fact]
        public void ShouldNotShipATaskSchedulerOfItsOwn()
        {
            Assert.DoesNotContain(ActiveMq.GetTypes(), type => typeof(TaskScheduler).IsAssignableFrom(type));
        }
    }
}
