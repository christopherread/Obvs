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

            A.CallTo(() => responseSource.Dispose()).MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => eventSource.Dispose()).MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => commandPublisher.Dispose()).MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => requestPublisher.Dispose()).MustHaveHappened(Repeated.Exactly.Once);
        }
        
        [Fact]
        public void ShouldDisposeEndpointClientIfWrappedInLoggingProxy()
        {
            IServiceEndpointClient endpointClient = A.Fake<IServiceEndpointClient>();
            IServiceEndpointClient loggingProxy = new ServiceEndpointClientLoggingProxy(A.Fake<ILoggerFactory>(), endpointClient, message => LogLevel.Info, message => LogLevel.Info);

            loggingProxy.Dispose();

            A.CallTo(() => endpointClient.Dispose()).MustHaveHappened(Repeated.Exactly.Once);
        }
    }
}