using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SQS;
using Amazon.SQS.Model;
using FakeItEasy;
using Obvs.MessageProperties;
using Xunit;

namespace Obvs.AmazonSqs.Tests
{
    // What settling a received message asks of SQS; the Explicit tests in Hosting check SQS does as asked
    public class TestMessageSettlement
    {
        private const string QueueUrl = "http://localhost:4566/000000000000/orders-commands";
        private const string DeadLetterQueueUrl = "http://localhost:4566/000000000000/orders-dead-letters";

        private readonly IAmazonSQS _sqs = A.Fake<IAmazonSQS>();

        [Fact]
        public async Task ShouldDeleteTheMessageWhenItsHandlerSucceeds()
        {
            var settlement = Settlement(receiveCount: 1);

            await settlement.CompleteAsync();

            A.CallTo(() => _sqs.DeleteMessageAsync(A<DeleteMessageRequest>.That.Matches(request =>
                    request.QueueUrl == QueueUrl && request.ReceiptHandle == "receipt-1"), A<CancellationToken>._))
                .MustHaveHappenedOnceExactly();
            Assert.True(settlement.Settled.IsCompleted);
        }

        [Fact]
        public async Task ShouldMakeTheMessageVisibleAgainWhenItsHandlerFails()
        {
            var settlement = Settlement(receiveCount: 1, maxDeliveryAttempts: 3);

            await settlement.AbandonAsync(new InvalidOperationException("Out of stock"));

            A.CallTo(() => _sqs.ChangeMessageVisibilityAsync(A<ChangeMessageVisibilityRequest>.That.Matches(request =>
                    request.QueueUrl == QueueUrl && request.ReceiptHandle == "receipt-1" && request.VisibilityTimeout == 0), A<CancellationToken>._))
                .MustHaveHappenedOnceExactly();
            A.CallTo(() => _sqs.DeleteMessageAsync(A<DeleteMessageRequest>._, A<CancellationToken>._)).MustNotHaveHappened();
            A.CallTo(() => _sqs.SendMessageAsync(A<SendMessageRequest>._, A<CancellationToken>._)).MustNotHaveHappened();
        }

        [Fact]
        public async Task ShouldMoveTheMessageToTheDeadLetterQueueWithTheReasonOnItsLastAttempt()
        {
            // Rather than leave it to the redrive policy, which would record only that the receive count was reached
            var settlement = Settlement(receiveCount: 3, maxDeliveryAttempts: 3);

            await settlement.AbandonAsync(new InvalidOperationException("Out of stock"));

            A.CallTo(() => _sqs.SendMessageAsync(A<SendMessageRequest>.That.Matches(request =>
                    request.QueueUrl == DeadLetterQueueUrl &&
                    request.MessageBody == "{\"OrderId\":\"o-1\"}" &&
                    request.MessageAttributes[MessagePropertyNames.TypeName].StringValue == "PlaceOrder" &&
                    request.MessageAttributes[MessageSettlement.ReasonAttribute].StringValue == MessageSettlement.HandlerFailed &&
                    request.MessageAttributes[MessageSettlement.DescriptionAttribute].StringValue.Contains("Out of stock") &&
                    request.MessageAttributes[MessageSettlement.ExceptionTypeAttribute].StringValue == "System.InvalidOperationException" &&
                    request.MessageAttributes[MessageSettlement.DeliveryAttemptsAttribute].StringValue == "3"), A<CancellationToken>._))
                .MustHaveHappenedOnceExactly()
                .Then(A.CallTo(() => _sqs.DeleteMessageAsync(A<DeleteMessageRequest>.That.Matches(request =>
                    request.QueueUrl == QueueUrl && request.ReceiptHandle == "receipt-1"), A<CancellationToken>._)).MustHaveHappenedOnceExactly());
            A.CallTo(() => _sqs.ChangeMessageVisibilityAsync(A<ChangeMessageVisibilityRequest>._, A<CancellationToken>._)).MustNotHaveHappened();
        }

        [Fact]
        public async Task ShouldMakeTheMessageVisibleAgainWhenTheBusStopsBeforeItsHandled()
        {
            var settlement = Settlement(receiveCount: 10, maxDeliveryAttempts: 10);

            await settlement.ReleaseAsync();

            A.CallTo(() => _sqs.ChangeMessageVisibilityAsync(A<ChangeMessageVisibilityRequest>.That.Matches(request =>
                    request.ReceiptHandle == "receipt-1" && request.VisibilityTimeout == 0), A<CancellationToken>._))
                .MustHaveHappenedOnceExactly();
            A.CallTo(() => _sqs.SendMessageAsync(A<SendMessageRequest>._, A<CancellationToken>._)).MustNotHaveHappened();
        }

        [Fact]
        public async Task ShouldSettleOnlyOnce()
        {
            // One message can reach several subscribers
            var settlement = Settlement(receiveCount: 1);

            await settlement.CompleteAsync();
            await settlement.AbandonAsync(new InvalidOperationException("Out of stock"));
            await settlement.ReleaseAsync();

            A.CallTo(() => _sqs.DeleteMessageAsync(A<DeleteMessageRequest>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
            A.CallTo(() => _sqs.ChangeMessageVisibilityAsync(A<ChangeMessageVisibilityRequest>._, A<CancellationToken>._)).MustNotHaveHappened();
        }

        [Fact]
        public async Task ShouldReportSettledOnceSettlingFinishesEvenIfSqsFails()
        {
            var settled = 0;
            A.CallTo(() => _sqs.DeleteMessageAsync(A<DeleteMessageRequest>._, A<CancellationToken>._)).ThrowsAsync(new AmazonSQSException("Unavailable"));
            var settlement = Settlement(receiveCount: 1, onSettled: () => Interlocked.Increment(ref settled));

            await Assert.ThrowsAsync<AmazonSQSException>(() => settlement.CompleteAsync());

            Assert.True(settlement.Settled.IsCompleted);
            Assert.Equal(1, settled);
        }

        [Fact]
        public void ShouldReportSettledWithoutAskingSqsWhenItStopsWaiting()
        {
            var settled = 0;
            var settlement = Settlement(receiveCount: 1, onSettled: () => Interlocked.Increment(ref settled));

            settlement.StopWaiting();
            settlement.StopWaiting();

            Assert.True(settlement.Settled.IsCompleted);
            Assert.Equal(1, settled);
            Assert.Empty(Fake.GetCalls(_sqs));
        }

        private MessageSettlement Settlement(int receiveCount, int maxDeliveryAttempts = 10, Action onSettled = null)
        {
            var message = new Message
            {
                MessageId = "message-1",
                ReceiptHandle = "receipt-1",
                Body = "{\"OrderId\":\"o-1\"}",
                Attributes = new Dictionary<string, string> { ["ApproximateReceiveCount"] = receiveCount.ToString() },
                MessageAttributes = new Dictionary<string, MessageAttributeValue>
                {
                    [MessagePropertyNames.TypeName] = new MessageAttributeValue { DataType = "String", StringValue = "PlaceOrder" },
                },
            };
            return new MessageSettlement(_sqs, QueueUrl, DeadLetterQueueUrl, message, maxDeliveryAttempts, onSettled ?? (() => { }));
        }
    }
}
