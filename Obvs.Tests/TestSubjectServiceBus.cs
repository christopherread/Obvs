using System;
using System.Collections.Generic;
using System.Threading;
using Obvs.Types;
using Xunit;

namespace Obvs.Tests
{
    
    public class TestSubjectServiceBus
    {
        private readonly SubjectServiceBus _testObj;

        public TestSubjectServiceBus()
        {
            _testObj = new SubjectServiceBus(new DefaultRequestCorrelationProvider());
        }

        [Fact]
        public void PublishBlocksUntilMessageHasBeenConsumed()
        {
            var subscriberThreadId = -1;
            _testObj.Events.Subscribe(_ => subscriberThreadId = Thread.CurrentThread.ManagedThreadId);

            var threadId = Thread.CurrentThread.ManagedThreadId;

            var task = _testObj.PublishAsync(new TestServiceEvent1());

            Assert.NotEqual(-1, subscriberThreadId);
            Assert.Equal(threadId, subscriberThreadId);

            task.Wait();
        }

        [Fact]
        public void SendBlocksUntilMessageHasBeenConsumed()
        {
            var subscriberThreadId = -1;
            _testObj.Commands.Subscribe(_ => subscriberThreadId = Thread.CurrentThread.ManagedThreadId);

            var threadId = Thread.CurrentThread.ManagedThreadId;

            var task = _testObj.SendAsync(new TestServiceCommand1());

            Assert.NotEqual(-1, subscriberThreadId);
            Assert.Equal(threadId, subscriberThreadId);

            task.Wait();
        }

        [Fact]
        public void GetResponsesReturnsResponseFromReply()
        {
            var requests = new List<IRequest>();
            var responses = new List<IResponse>();

            var sub1 = _testObj.Requests
                .Subscribe(req =>
                {
                    requests.Add(req);
                    _testObj.ReplyAsync(req, new TestServiceResponse1());
                });

            var sub2 = _testObj.GetResponses(new TestServiceRequest1())
                .Subscribe(res => responses.Add(res));

            Assert.NotEmpty(requests);
            Assert.IsType<TestServiceRequest1>(requests[0]);

            Assert.NotEmpty(responses);
            Assert.IsType<TestServiceResponse1>(responses[0]);

            sub1.Dispose();
            sub2.Dispose();
        }


        [Fact]
        public void PublishReturnsWithNoObservers()
        {
            Assert.True(_testObj.PublishAsync(new TestServiceEvent1()).Wait(1));
        }

        [Fact]
        public void SendReturnsWithNoObservers()
        {
            Assert.True(_testObj.SendAsync(new TestServiceCommand1()).Wait(1));
        }
    }
}