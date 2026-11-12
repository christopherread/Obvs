using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using FakeItEasy;
using Obvs.AzureSB.Tests.Hosting;
using Obvs.Serialization.Json;
using Xunit;

namespace Obvs.AzureSB.Tests
{
    public class TestMessagePublisherCancellation
    {
        [Fact]
        public async Task ShouldPassTheTokenToServiceBus()
        {
            var sender = A.Fake<ServiceBusSender>();
            var publisher = new MessagePublisher<IOrderMessage>(sender, new JsonMessageSerializer());
            using var cancellation = new CancellationTokenSource();

            await publisher.PublishAsync(new PlaceOrder { OrderId = "o-1" }, new Dictionary<string, string>(), cancellation.Token);

            A.CallTo(() => sender.SendMessageAsync(A<ServiceBusMessage>._, cancellation.Token)).MustHaveHappenedOnceExactly();
        }
    }
}
