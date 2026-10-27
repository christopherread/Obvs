using System.Threading;
using System.Threading.Tasks;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using FakeItEasy;
using Obvs.AmazonSqs.Tests.Hosting;
using Obvs.Serialization.Json;
using Xunit;

namespace Obvs.AmazonSqs.Tests
{
    public class TestMessagePublisherCancellation
    {
        [Fact]
        public async Task ShouldPassTheTokenToSqs()
        {
            var sqs = A.Fake<IAmazonSQS>();
            var publisher = new QueueMessagePublisher<IOrderMessage>(sqs, "https://sqs.local/orders-Commands", new JsonMessageSerializer());
            using var cancellation = new CancellationTokenSource();

            await publisher.PublishAsync(new PlaceOrder { OrderId = "o-1" }, cancellation.Token);

            A.CallTo(() => sqs.SendMessageAsync(A<SendMessageRequest>._, cancellation.Token)).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public async Task ShouldPassTheTokenToSns()
        {
            var sns = A.Fake<IAmazonSimpleNotificationService>();
            var publisher = new TopicMessagePublisher<IOrderMessage>(sns, "arn:aws:sns:us-east-1:000000000000:orders-Events", new JsonMessageSerializer());
            using var cancellation = new CancellationTokenSource();

            await publisher.PublishAsync(new OrderPlaced { OrderId = "o-1" }, cancellation.Token);

            A.CallTo(() => sns.PublishAsync(A<PublishRequest>._, cancellation.Token)).MustHaveHappenedOnceExactly();
        }
    }
}
