using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using FakeItEasy;
using Obvs.Logging;
using Obvs.Types;
using Xunit;

namespace Obvs.Tests
{
    
    public class TestServiceEndpointClient
    {
        [Fact]
        public void ShouldDisposeSourcesAndPublishers()
        {
            IMessageSource<IResponse> responseSource = A.Fake<IMessageSource<IResponse>>();
            IMessageSource<IEvent> eventSource = A.Fake<IMessageSource<IEvent>>();
            IMessagePublisher<ICommand> commandPublisher = A.Fake<IMessagePublisher<ICommand>>();
            IMessagePublisher<IRequest> requestPublisher = A.Fake<IMessagePublisher<IRequest>>();
            IServiceEndpointClient endpoint = new ServiceEndpointClient(eventSource, responseSource, requestPublisher, commandPublisher, typeof(ITestServiceMessage1));

            endpoint.Dispose();

            A.CallTo(() => responseSource.Dispose()).MustHaveHappenedOnceExactly();
            A.CallTo(() => eventSource.Dispose()).MustHaveHappenedOnceExactly();
            A.CallTo(() => commandPublisher.Dispose()).MustHaveHappenedOnceExactly();
            A.CallTo(() => requestPublisher.Dispose()).MustHaveHappenedOnceExactly();
        }
        
        [Fact]
        public async Task ShouldPassAFailedRequestSendToTheResponsesObserver()
        {
            var requestPublisher = A.Fake<IMessagePublisher<IRequest>>();
            A.CallTo(() => requestPublisher.PublishAsync(A<IRequest>._, A<CancellationToken>._)).ThrowsAsync(new InvalidOperationException("The broker refused the request"));
            IServiceEndpointClient endpoint = new ServiceEndpointClient(new DefaultMessageSource<IEvent>(), Never<IResponse>(), requestPublisher, A.Fake<IMessagePublisher<ICommand>>(), typeof(ITestServiceMessage1));

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => endpoint.GetResponses(new TestServiceRequest1()).FirstAsync().ToTask())
                .WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

            Assert.Equal("The broker refused the request", exception.Message);
        }

        [Fact]
        public async Task ShouldPassARequestSendThatThrowsToTheResponsesObserver()
        {
            var requestPublisher = A.Fake<IMessagePublisher<IRequest>>();
            A.CallTo(() => requestPublisher.PublishAsync(A<IRequest>._, A<CancellationToken>._)).Throws(new InvalidOperationException("Not connected"));
            IServiceEndpointClient endpoint = new ServiceEndpointClient(new DefaultMessageSource<IEvent>(), Never<IResponse>(), requestPublisher, A.Fake<IMessagePublisher<ICommand>>(), typeof(ITestServiceMessage1));

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => endpoint.GetResponses(new TestServiceRequest1()).FirstAsync().ToTask())
                .WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

            Assert.Equal("Not connected", exception.Message);
        }

        [Fact]
        public void ShouldSubscribeToResponsesBeforeSendingTheRequest()
        {
            var responses = new Subject<IResponse>();
            var responseSource = A.Fake<IMessageSource<IResponse>>();
            A.CallTo(() => responseSource.Messages).Returns(responses);
            var requestPublisher = A.Fake<IMessagePublisher<IRequest>>();
            var response = new TestServiceResponse1();
            A.CallTo(() => requestPublisher.PublishAsync(A<IRequest>._, A<CancellationToken>._)).Invokes(() => responses.OnNext(response)).Returns(Task.CompletedTask);
            IServiceEndpointClient endpoint = new ServiceEndpointClient(new DefaultMessageSource<IEvent>(), responseSource, requestPublisher, A.Fake<IMessagePublisher<ICommand>>(), typeof(ITestServiceMessage1));
            var received = new List<IResponse>();

            using (endpoint.GetResponses(new TestServiceRequest1()).Subscribe(received.Add))
            {
                Assert.Equal(new IResponse[] { response }, received);
            }
        }

        [Fact]
        public void ShouldStopSendingIfUnsubscribedBeforeTheSendFails()
        {
            var send = new TaskCompletionSource<bool>();
            var requestPublisher = A.Fake<IMessagePublisher<IRequest>>();
            A.CallTo(() => requestPublisher.PublishAsync(A<IRequest>._, A<CancellationToken>._)).Returns(send.Task);
            IServiceEndpointClient endpoint = new ServiceEndpointClient(new DefaultMessageSource<IEvent>(), Never<IResponse>(), requestPublisher, A.Fake<IMessagePublisher<ICommand>>(), typeof(ITestServiceMessage1));
            var errors = new List<Exception>();

            endpoint.GetResponses(new TestServiceRequest1()).Subscribe(_ => { }, errors.Add).Dispose();
            send.SetException(new InvalidOperationException("The broker refused the request"));

            Assert.Empty(errors);
        }

        private static IMessageSource<T> Never<T>() where T : class
        {
            var source = A.Fake<IMessageSource<T>>();
            A.CallTo(() => source.Messages).Returns(Observable.Never<T>());
            return source;
        }

        [Fact]
        public void ShouldDisposeEndpointClientIfWrappedInLoggingProxy()
        {
            IServiceEndpointClient endpointClient = A.Fake<IServiceEndpointClient>();
            IServiceEndpointClient loggingProxy = new ServiceEndpointClientLoggingProxy(A.Fake<ILoggerFactory>(), endpointClient, message => LogLevel.Info, message => LogLevel.Info);

            loggingProxy.Dispose();

            A.CallTo(() => endpointClient.Dispose()).MustHaveHappenedOnceExactly();
        }
    }
}