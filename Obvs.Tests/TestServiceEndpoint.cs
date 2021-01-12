using FakeItEasy;
using Obvs.Logging;
using Obvs.Types;
using Xunit;

namespace Obvs.Tests
{
    
    public class TestServiceEndpoint
    {
        [Fact]
        public void ShouldDisposeSourcesAndPublishers()
        {
            IMessageSource<IRequest> requestSource = A.Fake<IMessageSource<IRequest>>();
            IMessageSource<ICommand> commandSource = A.Fake<IMessageSource<ICommand>>();
            IMessagePublisher<IEvent> eventPublisher = A.Fake<IMessagePublisher<IEvent>>();
            IMessagePublisher<IResponse> responsePublisher = A.Fake<IMessagePublisher<IResponse>>();
            IServiceEndpoint endpoint = new ServiceEndpoint(requestSource, commandSource, eventPublisher, responsePublisher, typeof(ITestServiceMessage1));

            endpoint.Dispose();

            A.CallTo(() => requestSource.Dispose()).MustHaveHappenedOnceExactly();
            A.CallTo(() => commandSource.Dispose()).MustHaveHappenedOnceExactly();
            A.CallTo(() => eventPublisher.Dispose()).MustHaveHappenedOnceExactly();
            A.CallTo(() => responsePublisher.Dispose()).MustHaveHappenedOnceExactly();
        }


        [Fact]
        public void ShouldDisposeEndpointIfWrappedInLoggingProxy()
        {
            IServiceEndpoint endpoint = A.Fake<IServiceEndpoint>();
            IServiceEndpoint loggingProxy = new ServiceEndpointLoggingProxy(A.Fake<ILoggerFactory>(), endpoint, message => LogLevel.Info, message => LogLevel.Info);

            loggingProxy.Dispose();

            A.CallTo(() => endpoint.Dispose()).MustHaveHappenedOnceExactly();
        }
    }
}