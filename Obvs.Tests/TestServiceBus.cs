using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using FakeItEasy;
using Microsoft.Reactive.Testing;
using Obvs.Configuration;
using Obvs.Monitoring;
using Obvs.Types;
using Xunit;
using Xunit.Abstractions;

namespace Obvs.Tests
{
    
    public class TestServiceBus
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public TestServiceBus(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Fact]
        public void ShouldOnlySubscribeToUnderlyingEndpointRequestsOnce()
        {
            IServiceEndpoint serviceEndpoint1 = A.Fake<IServiceEndpoint>();
            IServiceEndpoint serviceEndpoint2 = A.Fake<IServiceEndpoint>();
            IServiceEndpointClient serviceEndpointClient1 = A.Fake<IServiceEndpointClient>();
            IServiceEndpointClient serviceEndpointClient2 = A.Fake<IServiceEndpointClient>();

            IObservable<IRequest> observable1 = A.Fake<IObservable<IRequest>>();
            IObservable<IRequest> observable2 = A.Fake<IObservable<IRequest>>();

            IObserver<IRequest> observer1 = A.Fake<IObserver<IRequest>>();
            IObserver<IRequest> observer2 = A.Fake<IObserver<IRequest>>();

            A.CallTo(() => serviceEndpoint1.Requests).Returns(observable1);
            A.CallTo(() => serviceEndpoint2.Requests).Returns(observable2);

            IServiceBus serviceBus = new ServiceBus(new[] { serviceEndpointClient1, serviceEndpointClient2 }, new[] { serviceEndpoint1, serviceEndpoint2 } );

            IDisposable sub1 = serviceBus.Requests.Subscribe(observer1);
            IDisposable sub2 = serviceBus.Requests.Subscribe(observer2);
            
            A.CallTo(() => serviceEndpoint1.Requests).MustHaveHappenedOnceExactly();
            A.CallTo(() => serviceEndpoint2.Requests).MustHaveHappenedOnceExactly();

            A.CallTo(() => observable1.Subscribe(A<IObserver<IRequest>>._)).MustHaveHappenedOnceExactly();
            A.CallTo(() => observable2.Subscribe(A<IObserver<IRequest>>._)).MustHaveHappenedOnceExactly();

            sub1.Dispose();
            sub2.Dispose();
        }
        
        [Fact]
        public void ShouldOnlySubscribeToUnderlyingEndpointCommandsOnce()
        {
            IServiceEndpoint serviceEndpoint1 = A.Fake<IServiceEndpoint>();
            IServiceEndpoint serviceEndpoint2 = A.Fake<IServiceEndpoint>();
            IServiceEndpointClient serviceEndpointClient1 = A.Fake<IServiceEndpointClient>();
            IServiceEndpointClient serviceEndpointClient2 = A.Fake<IServiceEndpointClient>();

            IObservable<ICommand> observable1 = A.Fake<IObservable<ICommand>>();
            IObservable<ICommand> observable2 = A.Fake<IObservable<ICommand>>();

            IObserver<ICommand> observer1 = A.Fake<IObserver<ICommand>>();
            IObserver<ICommand> observer2 = A.Fake<IObserver<ICommand>>();

            A.CallTo(() => serviceEndpoint1.Commands).Returns(observable1);
            A.CallTo(() => serviceEndpoint2.Commands).Returns(observable2);

            IServiceBus serviceBus = new ServiceBus(new[] { serviceEndpointClient1, serviceEndpointClient2 }, new[] { serviceEndpoint1, serviceEndpoint2 });

            IDisposable sub1 = serviceBus.Commands.Subscribe(observer1);
            IDisposable sub2 = serviceBus.Commands.Subscribe(observer2);
            
            A.CallTo(() => serviceEndpoint1.Commands).MustHaveHappenedOnceExactly();
            A.CallTo(() => serviceEndpoint2.Commands).MustHaveHappenedOnceExactly();

            A.CallTo(() => observable1.Subscribe(A<IObserver<ICommand>>._)).MustHaveHappenedOnceExactly();
            A.CallTo(() => observable2.Subscribe(A<IObserver<ICommand>>._)).MustHaveHappenedOnceExactly();

            sub1.Dispose();
            sub2.Dispose();
        }
        
        [Fact]
        public void ShouldOnlySubscribeToUnderlyingEndpointEventsOnce()
        {
            IServiceEndpoint serviceEndpoint1 = A.Fake<IServiceEndpoint>();
            IServiceEndpoint serviceEndpoint2 = A.Fake<IServiceEndpoint>();
            IServiceEndpointClient serviceEndpointClient1 = A.Fake<IServiceEndpointClient>();
            IServiceEndpointClient serviceEndpointClient2 = A.Fake<IServiceEndpointClient>();

            IObservable<IEvent> observable1 = A.Fake<IObservable<IEvent>>();
            IObservable<IEvent> observable2 = A.Fake<IObservable<IEvent>>();

            IObserver<IEvent> observer1 = A.Fake<IObserver<IEvent>>();
            IObserver<IEvent> observer2 = A.Fake<IObserver<IEvent>>();

            A.CallTo(() => serviceEndpointClient1.Events).Returns(observable1);
            A.CallTo(() => serviceEndpointClient2.Events).Returns(observable2);

            IServiceBus serviceBus = new ServiceBus(new[] { serviceEndpointClient1, serviceEndpointClient2 }, new[] { serviceEndpoint1, serviceEndpoint2 });

            IDisposable sub1 = serviceBus.Events.Subscribe(observer1);
            IDisposable sub2 = serviceBus.Events.Subscribe(observer2);
            sub1.Dispose();
            IDisposable sub3 = serviceBus.Events.Subscribe(observer1);
            IDisposable sub4 = serviceBus.Events.Subscribe(observer2);
            
            A.CallTo(() => observable1.Subscribe(A<IObserver<IEvent>>._)).MustHaveHappenedOnceExactly();
            A.CallTo(() => observable2.Subscribe(A<IObserver<IEvent>>._)).MustHaveHappenedOnceExactly();

            sub2.Dispose();
            sub3.Dispose();
            sub4.Dispose();
        }
        
        [Fact]
        public void ShouldOnlySubscribeToUnderlyingMessageSourceEventsOnce()
        {
            var messageSource1 = A.Fake<IMessageSource<IEvent>>();
            var messageSource2 = A.Fake<IMessageSource<IEvent>>();

            IServiceEndpointClient serviceEndpointClient1 = new ServiceEndpointClient(
                messageSource1,
                A.Fake<IMessageSource<IResponse>>(), 
                A.Fake<IMessagePublisher<IRequest>>(),
                A.Fake<IMessagePublisher<ICommand>>(),
                typeof(IMessage));
           
            IServiceEndpointClient serviceEndpointClient2 = new ServiceEndpointClient(
                messageSource2,
                A.Fake<IMessageSource<IResponse>>(),
                A.Fake<IMessagePublisher<IRequest>>(),
                A.Fake<IMessagePublisher<ICommand>>(),
                typeof(IMessage));

            int subscribed1 = 0;
            int subscribed2 = 0;
            int disposed1 = 0;
            int disposed2 = 0;

            IObservable<IEvent> observable1 = Observable.Create<IEvent>(observer =>
            {
                subscribed1++;
                return Disposable.Create(() => disposed1++);
            });

            IObservable<IEvent> observable2 = Observable.Create<IEvent>(observer =>
            {
                subscribed2++;
                return Disposable.Create(() => disposed2++);
            });

            A.CallTo(() => messageSource1.Messages).Returns(observable1);
            A.CallTo(() => messageSource2.Messages).Returns(observable2);

            IServiceBus serviceBus = new ServiceBus(new[] { serviceEndpointClient1, serviceEndpointClient2 }, new IServiceEndpoint[0]);

            IObserver<IEvent> observer1 = A.Fake<IObserver<IEvent>>();
            IObserver<IEvent> observer2 = A.Fake<IObserver<IEvent>>();
            IObserver<IEvent> observer3 = A.Fake<IObserver<IEvent>>();
            IObserver<IEvent> observer4 = A.Fake<IObserver<IEvent>>();

            IDisposable sub1 = serviceBus.Events.Subscribe(observer1);
            IDisposable sub2 = serviceBus.Events.Subscribe(observer2);
            sub1.Dispose();
            IDisposable sub3 = serviceBus.Events.Subscribe(observer3);
            IDisposable sub4 = serviceBus.Events.Subscribe(observer4);
            
            Assert.Equal(subscribed1, 1);
            Assert.Equal(subscribed2, 1);
            Assert.Equal(disposed1, 0);
            Assert.Equal(disposed2, 0);
            
            sub2.Dispose();
            sub3.Dispose();
            sub4.Dispose();

            Assert.Equal(disposed1, 1);
            Assert.Equal(disposed2, 1);
        }
        
        [Fact]
        public void ShouldOnlySubscribeToUnderlyingEndpointResponsesOnce()
        {
            IServiceEndpoint serviceEndpoint1 = A.Fake<IServiceEndpoint>();
            IServiceEndpoint serviceEndpoint2 = A.Fake<IServiceEndpoint>();
            IServiceEndpointClient serviceEndpointClient1 = A.Fake<IServiceEndpointClient>();
            IServiceEndpointClient serviceEndpointClient2 = A.Fake<IServiceEndpointClient>();

            IObservable<IResponse> observable1 = A.Fake<IObservable<IResponse>>();
            IObservable<IResponse> observable2 = A.Fake<IObservable<IResponse>>();

            IObserver<IResponse> observer1 = A.Fake<IObserver<IResponse>>();
            IObserver<IResponse> observer2 = A.Fake<IObserver<IResponse>>();

            IRequest request = A.Fake<IRequest>();

            A.CallTo(() => serviceEndpointClient1.CanHandle(request)).Returns(true);
            A.CallTo(() => serviceEndpointClient2.CanHandle(request)).Returns(true);

            A.CallTo(() => serviceEndpointClient1.GetResponses(request)).Returns(observable1);
            A.CallTo(() => serviceEndpointClient2.GetResponses(request)).Returns(observable2);

            IServiceBus serviceBus = new ServiceBus(new[] { serviceEndpointClient1, serviceEndpointClient2 }, new[] { serviceEndpoint1, serviceEndpoint2 });

            IObservable<IResponse> responses = serviceBus.GetResponses(request);

            IDisposable sub1 = responses.Subscribe(observer1);
            IDisposable sub2 = responses.Subscribe(observer2);

            A.CallTo(() => serviceEndpointClient1.GetResponses(request)).MustHaveHappenedOnceExactly();
            A.CallTo(() => serviceEndpointClient2.GetResponses(request)).MustHaveHappenedOnceExactly();

            A.CallTo(() => observable1.Subscribe(A<IObserver<IResponse>>._)).MustHaveHappenedOnceExactly();
            A.CallTo(() => observable2.Subscribe(A<IObserver<IResponse>>._)).MustHaveHappenedOnceExactly();

            sub1.Dispose();
            sub2.Dispose();
        }

        [Fact]
        public void ShouldReturnRequestsFromUnderlyingEndpoints()
        {
            IServiceEndpoint serviceEndpoint1 = A.Fake<IServiceEndpoint>();
            IServiceEndpoint serviceEndpoint2 = A.Fake<IServiceEndpoint>();
            IServiceEndpointClient serviceEndpointClient1 = A.Fake<IServiceEndpointClient>();
            IServiceEndpointClient serviceEndpointClient2 = A.Fake<IServiceEndpointClient>();

            IObservable<IRequest> observable1 = A.Fake<IObservable<IRequest>>();
            IObservable<IRequest> observable2 = A.Fake<IObservable<IRequest>>();

            IObserver<IRequest> observer1 = A.Fake<IObserver<IRequest>>();
            IObserver<IRequest> observer2 = A.Fake<IObserver<IRequest>>();

            A.CallTo(() => serviceEndpoint1.Requests).Returns(observable1);
            A.CallTo(() => serviceEndpoint2.Requests).Returns(observable2);

            IObserver<IRequest> internalObserver1 = null;
            IObserver<IRequest> internalObserver2 = null;

            A.CallTo(() => observable1.Subscribe(A<IObserver<IRequest>>._)).Invokes(call => internalObserver1 = call.GetArgument<IObserver<IRequest>>(0));
            A.CallTo(() => observable2.Subscribe(A<IObserver<IRequest>>._)).Invokes(call => internalObserver2 = call.GetArgument<IObserver<IRequest>>(0));

            IServiceBus serviceBus = new ServiceBus(new[] { serviceEndpointClient1, serviceEndpointClient2 }, new[] { serviceEndpoint1, serviceEndpoint2 });

            IDisposable sub1 = serviceBus.Requests.Subscribe(observer1);
            IDisposable sub2 = serviceBus.Requests.Subscribe(observer2);

            Assert.NotNull(internalObserver1);
            Assert.NotNull(internalObserver2);

            IRequest request1 = A.Fake<IRequest>();
            IRequest request2 = A.Fake<IRequest>();
            internalObserver1.OnNext(request1);
            internalObserver2.OnNext(request2);

            A.CallTo(() => observer1.OnNext(request1)).MustHaveHappenedOnceExactly();
            A.CallTo(() => observer2.OnNext(request1)).MustHaveHappenedOnceExactly();
            A.CallTo(() => observer1.OnNext(request2)).MustHaveHappenedOnceExactly();
            A.CallTo(() => observer2.OnNext(request2)).MustHaveHappenedOnceExactly();

            sub1.Dispose();
            sub2.Dispose();
        }
        
        [Fact]
        public void ShouldReturnCommandsFromUnderlyingEndpoints()
        {
            IServiceEndpoint serviceEndpoint1 = A.Fake<IServiceEndpoint>();
            IServiceEndpoint serviceEndpoint2 = A.Fake<IServiceEndpoint>();
            IServiceEndpointClient serviceEndpointClient1 = A.Fake<IServiceEndpointClient>();
            IServiceEndpointClient serviceEndpointClient2 = A.Fake<IServiceEndpointClient>();

            IObservable<ICommand> observable1 = A.Fake<IObservable<ICommand>>();
            IObservable<ICommand> observable2 = A.Fake<IObservable<ICommand>>();

            IObserver<ICommand> observer1 = A.Fake<IObserver<ICommand>>();
            IObserver<ICommand> observer2 = A.Fake<IObserver<ICommand>>();

            A.CallTo(() => serviceEndpoint1.Commands).Returns(observable1);
            A.CallTo(() => serviceEndpoint2.Commands).Returns(observable2);

            IObserver<ICommand> internalObserver1 = null;
            IObserver<ICommand> internalObserver2 = null;

            A.CallTo(() => observable1.Subscribe(A<IObserver<ICommand>>._)).Invokes(call => internalObserver1 = call.GetArgument<IObserver<ICommand>>(0));
            A.CallTo(() => observable2.Subscribe(A<IObserver<ICommand>>._)).Invokes(call => internalObserver2 = call.GetArgument<IObserver<ICommand>>(0));

            IServiceBus serviceBus = new ServiceBus(new[] { serviceEndpointClient1, serviceEndpointClient2 }, new[] { serviceEndpoint1, serviceEndpoint2 });

            IDisposable sub1 = serviceBus.Commands.Subscribe(observer1);
            IDisposable sub2 = serviceBus.Commands.Subscribe(observer2);

            Assert.NotNull(internalObserver1);
            Assert.NotNull(internalObserver2);

            ICommand command1 = A.Fake<ICommand>();
            ICommand command2 = A.Fake<ICommand>();
            internalObserver1.OnNext(command1);
            internalObserver2.OnNext(command2);

            A.CallTo(() => observer1.OnNext(command1)).MustHaveHappenedOnceExactly();
            A.CallTo(() => observer2.OnNext(command1)).MustHaveHappenedOnceExactly();
            A.CallTo(() => observer1.OnNext(command2)).MustHaveHappenedOnceExactly();
            A.CallTo(() => observer2.OnNext(command2)).MustHaveHappenedOnceExactly();

            sub1.Dispose();
            sub2.Dispose();
        }
        
        [Fact]
        public void ShouldReturnEventsFromUnderlyingEndpoints()
        {
            IServiceEndpoint serviceEndpoint1 = A.Fake<IServiceEndpoint>();
            IServiceEndpoint serviceEndpoint2 = A.Fake<IServiceEndpoint>();
            IServiceEndpointClient serviceEndpointClient1 = A.Fake<IServiceEndpointClient>();
            IServiceEndpointClient serviceEndpointClient2 = A.Fake<IServiceEndpointClient>();

            IObservable<IEvent> observable1 = A.Fake<IObservable<IEvent>>();
            IObservable<IEvent> observable2 = A.Fake<IObservable<IEvent>>();

            IObserver<IEvent> observer1 = A.Fake<IObserver<IEvent>>();
            IObserver<IEvent> observer2 = A.Fake<IObserver<IEvent>>();

            A.CallTo(() => serviceEndpointClient1.Events).Returns(observable1);
            A.CallTo(() => serviceEndpointClient2.Events).Returns(observable2);

            IObserver<IEvent> internalObserver1 = null;
            IObserver<IEvent> internalObserver2 = null;

            A.CallTo(() => observable1.Subscribe(A<IObserver<IEvent>>._)).Invokes(call => internalObserver1 = call.GetArgument<IObserver<IEvent>>(0));
            A.CallTo(() => observable2.Subscribe(A<IObserver<IEvent>>._)).Invokes(call => internalObserver2 = call.GetArgument<IObserver<IEvent>>(0));

            IServiceBus serviceBus = new ServiceBus(new[] { serviceEndpointClient1, serviceEndpointClient2 }, new[] { serviceEndpoint1, serviceEndpoint2 });

            IDisposable sub1 = serviceBus.Events.Subscribe(observer1);
            IDisposable sub2 = serviceBus.Events.Subscribe(observer2);

            Assert.NotNull(internalObserver1);
            Assert.NotNull(internalObserver2);

            IEvent event1 = A.Fake<IEvent>();
            IEvent event2 = A.Fake<IEvent>();
            internalObserver1.OnNext(event1);
            internalObserver2.OnNext(event2);

            A.CallTo(() => observer1.OnNext(event1)).MustHaveHappenedOnceExactly();
            A.CallTo(() => observer2.OnNext(event1)).MustHaveHappenedOnceExactly();
            A.CallTo(() => observer1.OnNext(event2)).MustHaveHappenedOnceExactly();
            A.CallTo(() => observer2.OnNext(event2)).MustHaveHappenedOnceExactly();

            sub1.Dispose();
            sub2.Dispose();
        }
        
        [Fact]
        public void ShouldReturnResponsesFromUnderlyingEndpoints()
        {
            IServiceEndpoint serviceEndpoint1 = A.Fake<IServiceEndpoint>();
            IServiceEndpoint serviceEndpoint2 = A.Fake<IServiceEndpoint>();
            IServiceEndpointClient serviceEndpointClient1 = A.Fake<IServiceEndpointClient>();
            IServiceEndpointClient serviceEndpointClient2 = A.Fake<IServiceEndpointClient>();

            IObservable<IResponse> observable1 = A.Fake<IObservable<IResponse>>();
            IObservable<IResponse> observable2 = A.Fake<IObservable<IResponse>>();

            IObserver<IResponse> observer1 = A.Fake<IObserver<IResponse>>();
            IObserver<IResponse> observer2 = A.Fake<IObserver<IResponse>>();

            IRequest request = A.Fake<IRequest>();

            A.CallTo(() => serviceEndpointClient1.GetResponses(request)).Returns(observable1);
            A.CallTo(() => serviceEndpointClient2.GetResponses(request)).Returns(observable2);
            
            A.CallTo(() => serviceEndpointClient1.CanHandle(request)).Returns(true);
            A.CallTo(() => serviceEndpointClient2.CanHandle(request)).Returns(true);

            IObserver<IResponse> internalObserver1 = null;
            IObserver<IResponse> internalObserver2 = null;

            A.CallTo(() => observable1.Subscribe(A<IObserver<IResponse>>._)).Invokes(call => internalObserver1 = call.GetArgument<IObserver<IResponse>>(0));
            A.CallTo(() => observable2.Subscribe(A<IObserver<IResponse>>._)).Invokes(call => internalObserver2 = call.GetArgument<IObserver<IResponse>>(0));

            IServiceBus serviceBus = new ServiceBus(new[] { serviceEndpointClient1, serviceEndpointClient2 }, new[] { serviceEndpoint1, serviceEndpoint2 });

            IObservable<IResponse> responses = serviceBus.GetResponses(request);
            IDisposable sub1 = responses.Subscribe(observer1);
            IDisposable sub2 = responses.Subscribe(observer2);

            Assert.NotNull(internalObserver1);
            Assert.NotNull(internalObserver2);

            // ensure id's on responses match those set on the request
            IResponse response1 = A.Fake<IResponse>();
            IResponse response2 = A.Fake<IResponse>();
            response1.RequestId = request.RequestId;
            response2.RequestId = request.RequestId;
            response1.RequesterId = request.RequesterId;
            response2.RequesterId = request.RequesterId;

            internalObserver1.OnNext(response1);
            internalObserver2.OnNext(response2);

            A.CallTo(() => observer1.OnNext(response1)).MustHaveHappenedOnceExactly();
            A.CallTo(() => observer2.OnNext(response1)).MustHaveHappenedOnceExactly();
            A.CallTo(() => observer1.OnNext(response1)).MustHaveHappenedOnceExactly();
            A.CallTo(() => observer2.OnNext(response2)).MustHaveHappenedOnceExactly();

            sub1.Dispose();
            sub2.Dispose();
        }

        [Fact]
        public void ShouldDisposeUnderlyingRequestSubscriptionOnlyWhenAllSubscriptionsDisposed()
        {
            IServiceEndpoint serviceEndpoint1 = A.Fake<IServiceEndpoint>();
            IServiceEndpoint serviceEndpoint2 = A.Fake<IServiceEndpoint>();
            IServiceEndpointClient serviceEndpointClient1 = A.Fake<IServiceEndpointClient>();
            IServiceEndpointClient serviceEndpointClient2 = A.Fake<IServiceEndpointClient>();

            IObservable<IRequest> observable1 = A.Fake<IObservable<IRequest>>();
            IObservable<IRequest> observable2 = A.Fake<IObservable<IRequest>>();

            IObserver<IRequest> observer1 = A.Fake<IObserver<IRequest>>();
            IObserver<IRequest> observer2 = A.Fake<IObserver<IRequest>>();

            A.CallTo(() => serviceEndpoint1.Requests).Returns(observable1);
            A.CallTo(() => serviceEndpoint2.Requests).Returns(observable2);

            IObserver<IRequest> requestSource1 = null;
            IObserver<IRequest> requestSource2 = null;

            A.CallTo(() => observable1.Subscribe(A<IObserver<IRequest>>._)).Invokes(call => requestSource1 = call.GetArgument<IObserver<IRequest>>(0));
            A.CallTo(() => observable2.Subscribe(A<IObserver<IRequest>>._)).Invokes(call => requestSource2 = call.GetArgument<IObserver<IRequest>>(0));

            IServiceBus serviceBus = new ServiceBus(new[] { serviceEndpointClient1, serviceEndpointClient2 }, new[] { serviceEndpoint1, serviceEndpoint2 });

            IDisposable requestSub1 = serviceBus.Requests.Subscribe(observer1);
            IDisposable requestSub2 = serviceBus.Requests.Subscribe(observer2);

            Assert.NotNull(requestSource1);
            Assert.NotNull(requestSource2);

            IRequest request = A.Fake<IRequest>();

            requestSource1.OnNext(request);
            A.CallTo(() => observer1.OnNext(request)).MustHaveHappenedOnceExactly();
            A.CallTo(() => observer2.OnNext(request)).MustHaveHappenedOnceExactly();

            // dispose of first subscriptions
            requestSub1.Dispose();

            // second subscription should still be active
            requestSource1.OnNext(request);
            A.CallTo(() => observer1.OnNext(request)).MustHaveHappenedOnceExactly();
            A.CallTo(() => observer2.OnNext(request)).MustHaveHappenedTwiceExactly();

            // dispose of second subscriptions
            requestSub2.Dispose();
            
            // no subscriptions should be active
            requestSource1.OnNext(request);
            A.CallTo(() => observer1.OnNext(request)).MustHaveHappenedOnceExactly();
            A.CallTo(() => observer2.OnNext(request)).MustHaveHappenedTwiceExactly();
        }

        [Fact]
        public void ShouldDisposeUnderlyingCommandSubscriptionOnlyWhenAllSubscriptionsDisposed()
        {
            IServiceEndpoint serviceEndpoint1 = A.Fake<IServiceEndpoint>();
            IServiceEndpoint serviceEndpoint2 = A.Fake<IServiceEndpoint>();
            IServiceEndpointClient serviceEndpointClient1 = A.Fake<IServiceEndpointClient>();
            IServiceEndpointClient serviceEndpointClient2 = A.Fake<IServiceEndpointClient>();

            IObservable<ICommand> observable1 = A.Fake<IObservable<ICommand>>();
            IObservable<ICommand> observable2 = A.Fake<IObservable<ICommand>>();

            IObserver<ICommand> observer1 = A.Fake<IObserver<ICommand>>();
            IObserver<ICommand> observer2 = A.Fake<IObserver<ICommand>>();

            A.CallTo(() => serviceEndpoint1.Commands).Returns(observable1);
            A.CallTo(() => serviceEndpoint2.Commands).Returns(observable2);

            IObserver<ICommand> commandSource1 = null;
            IObserver<ICommand> commandSource2 = null;

            A.CallTo(() => observable1.Subscribe(A<IObserver<ICommand>>._)).Invokes(call => commandSource1 = call.GetArgument<IObserver<ICommand>>(0));
            A.CallTo(() => observable2.Subscribe(A<IObserver<ICommand>>._)).Invokes(call => commandSource2 = call.GetArgument<IObserver<ICommand>>(0));

            IServiceBus serviceBus = new ServiceBus(new[] { serviceEndpointClient1, serviceEndpointClient2 }, new[] { serviceEndpoint1, serviceEndpoint2 });

            IDisposable commandSub1 = serviceBus.Commands.Subscribe(observer1);
            IDisposable commandSub2 = serviceBus.Commands.Subscribe(observer2);

            Assert.NotNull(commandSource1);
            Assert.NotNull(commandSource2);

            ICommand command = A.Fake<ICommand>();

            commandSource1.OnNext(command);
            A.CallTo(() => observer1.OnNext(command)).MustHaveHappenedOnceExactly();
            A.CallTo(() => observer2.OnNext(command)).MustHaveHappenedOnceExactly();

            // dispose of first subscriptions
            commandSub1.Dispose();

            // second subscription should still be active
            commandSource1.OnNext(command);
            A.CallTo(() => observer1.OnNext(command)).MustHaveHappenedOnceExactly();
            A.CallTo(() => observer2.OnNext(command)).MustHaveHappenedTwiceExactly();

            // dispose of second subscriptions
            commandSub2.Dispose();
            
            // no subscriptions should be active
            commandSource1.OnNext(command);
            A.CallTo(() => observer1.OnNext(command)).MustHaveHappenedOnceExactly();
            A.CallTo(() => observer2.OnNext(command)).MustHaveHappenedTwiceExactly();
        }

        [Fact]
        public void ShouldDisposeUnderlyingEventSubscriptionOnlyWhenAllSubscriptionsDisposed()
        {
            IServiceEndpoint serviceEndpoint1 = A.Fake<IServiceEndpoint>();
            IServiceEndpoint serviceEndpoint2 = A.Fake<IServiceEndpoint>();
            IServiceEndpointClient serviceEndpointClient1 = A.Fake<IServiceEndpointClient>();
            IServiceEndpointClient serviceEndpointClient2 = A.Fake<IServiceEndpointClient>();

            IObservable<IEvent> observable1 = A.Fake<IObservable<IEvent>>();
            IObservable<IEvent> observable2 = A.Fake<IObservable<IEvent>>();

            IObserver<IEvent> observer1 = A.Fake<IObserver<IEvent>>();
            IObserver<IEvent> observer2 = A.Fake<IObserver<IEvent>>();

            A.CallTo(() => serviceEndpointClient1.Events).Returns(observable1);
            A.CallTo(() => serviceEndpointClient2.Events).Returns(observable2);

            IObserver<IEvent> eventSource1 = null;
            IObserver<IEvent> eventSource2 = null;

            A.CallTo(() => observable1.Subscribe(A<IObserver<IEvent>>._)).Invokes(call => eventSource1 = call.GetArgument<IObserver<IEvent>>(0));
            A.CallTo(() => observable2.Subscribe(A<IObserver<IEvent>>._)).Invokes(call => eventSource2 = call.GetArgument<IObserver<IEvent>>(0));

            IServiceBus serviceBus = new ServiceBus(new[] { serviceEndpointClient1, serviceEndpointClient2 }, new[] { serviceEndpoint1, serviceEndpoint2 });

            IDisposable eventSub1 = serviceBus.Events.Subscribe(observer1);
            IDisposable eventSub2 = serviceBus.Events.Subscribe(observer2);

            Assert.NotNull(eventSource1);
            Assert.NotNull(eventSource2);

            IEvent ev = A.Fake<IEvent>();

            eventSource1.OnNext(ev);
            A.CallTo(() => observer1.OnNext(ev)).MustHaveHappenedOnceExactly();
            A.CallTo(() => observer2.OnNext(ev)).MustHaveHappenedOnceExactly();

            // dispose of first subscriptions
            eventSub1.Dispose();

            // second subscription should still be active
            eventSource1.OnNext(ev);
            A.CallTo(() => observer1.OnNext(ev)).MustHaveHappenedOnceExactly();
            A.CallTo(() => observer2.OnNext(ev)).MustHaveHappenedTwiceExactly();

            // dispose of second subscriptions
            eventSub2.Dispose();
            
            // no subscriptions should be active
            eventSource1.OnNext(ev);
            A.CallTo(() => observer1.OnNext(ev)).MustHaveHappenedOnceExactly();
            A.CallTo(() => observer2.OnNext(ev)).MustHaveHappenedTwiceExactly();
        }

        [Fact]
        public void ShouldHandleUnderlyingEventSubscriptionErrorsOnTheExceptionChannel()
        {
            IServiceEndpoint serviceEndpoint1 = A.Fake<IServiceEndpoint>();
            IServiceEndpoint serviceEndpoint2 = A.Fake<IServiceEndpoint>();
            IServiceEndpointClient serviceEndpointClient1 = A.Fake<IServiceEndpointClient>();
            IServiceEndpointClient serviceEndpointClient2 = A.Fake<IServiceEndpointClient>();

            IObservable<IEvent> observable1 = A.Fake<IObservable<IEvent>>();
            IObservable<IEvent> observable2 = A.Fake<IObservable<IEvent>>();

            IObserver<IEvent> observer1 = A.Fake<IObserver<IEvent>>();
            IObserver<IEvent> observer2 = A.Fake<IObserver<IEvent>>();
            IObserver<Exception> observer3 = A.Fake<IObserver<Exception>>();

            A.CallTo(() => serviceEndpointClient1.Events).Returns(observable1);
            A.CallTo(() => serviceEndpointClient2.Events).Returns(observable2);

            IObserver<IEvent> eventSource1 = null;
            IObserver<IEvent> eventSource2 = null;

            A.CallTo(() => observable1.Subscribe(A<IObserver<IEvent>>._)).Invokes(call => eventSource1 = call.GetArgument<IObserver<IEvent>>(0));
            A.CallTo(() => observable2.Subscribe(A<IObserver<IEvent>>._)).Invokes(call => eventSource2 = call.GetArgument<IObserver<IEvent>>(0));

            IServiceBus serviceBus = new ServiceBus(new[] { serviceEndpointClient1, serviceEndpointClient2 }, new[] { serviceEndpoint1, serviceEndpoint2 });

            IDisposable eventSub1 = serviceBus.Events.Subscribe(observer1);
            IDisposable eventSub2 = serviceBus.Events.Subscribe(observer2);
            IDisposable exceptionSub = serviceBus.Exceptions.Subscribe(observer3);

            Assert.NotNull(eventSource1);
            Assert.NotNull(eventSource2);

            IEvent event1 = A.Fake<IEvent>();
            IEvent event2 = A.Fake<IEvent>();

            eventSource1.OnNext(event1);
            A.CallTo(() => observer1.OnNext(event1)).MustHaveHappenedOnceExactly();
            A.CallTo(() => observer2.OnNext(event1)).MustHaveHappenedOnceExactly();

            Exception exception = new Exception();
            eventSource1.OnError(exception);
            eventSource1.OnError(new Exception());
            eventSource1.OnError(new Exception());
            eventSource1.OnError(new Exception());
            A.CallTo(() => observer1.OnError(exception)).MustNotHaveHappened();
            A.CallTo(() => observer2.OnError(exception)).MustNotHaveHappened();

            eventSource2.OnNext(event2);
            A.CallTo(() => observer1.OnNext(event2)).MustHaveHappenedOnceExactly();
            A.CallTo(() => observer2.OnNext(event2)).MustHaveHappenedOnceExactly();

            A.CallTo(() => observer3.OnNext(A<Exception>._)).WhenArgumentsMatch(call => call.Get<Exception>(0).InnerException == exception).MustHaveHappenedOnceExactly();

            eventSub1.Dispose();
            eventSub2.Dispose();
            exceptionSub.Dispose();
        }

        [Fact]
        public void ShouldHandleUnderlyingCommandSubscriptionErrorsOnTheExceptionChannel()
        {
            IServiceEndpoint serviceEndpoint1 = A.Fake<IServiceEndpoint>();
            IServiceEndpoint serviceEndpoint2 = A.Fake<IServiceEndpoint>();
            IServiceEndpointClient serviceEndpointClient1 = A.Fake<IServiceEndpointClient>();
            IServiceEndpointClient serviceEndpointClient2 = A.Fake<IServiceEndpointClient>();

            IObservable<ICommand> observable1 = A.Fake<IObservable<ICommand>>();
            IObservable<ICommand> observable2 = A.Fake<IObservable<ICommand>>();

            IObserver<ICommand> observer1 = A.Fake<IObserver<ICommand>>();
            IObserver<ICommand> observer2 = A.Fake<IObserver<ICommand>>();
            IObserver<Exception> observer3 = A.Fake<IObserver<Exception>>();

            A.CallTo(() => serviceEndpoint1.Commands).Returns(observable1);
            A.CallTo(() => serviceEndpoint2.Commands).Returns(observable2);

            IObserver<ICommand> commandSource1 = null;
            IObserver<ICommand> commandSource2 = null;

            A.CallTo(() => observable1.Subscribe(A<IObserver<ICommand>>._)).Invokes(call => commandSource1 = call.GetArgument<IObserver<ICommand>>(0));
            A.CallTo(() => observable2.Subscribe(A<IObserver<ICommand>>._)).Invokes(call => commandSource2 = call.GetArgument<IObserver<ICommand>>(0));

            IServiceBus serviceBus = new ServiceBus(new[] { serviceEndpointClient1, serviceEndpointClient2 }, new[] { serviceEndpoint1, serviceEndpoint2 });

            IDisposable eventSub1 = serviceBus.Commands.Subscribe(observer1);
            IDisposable eventSub2 = serviceBus.Commands.Subscribe(observer2);
            IDisposable exceptionSub = serviceBus.Exceptions.Subscribe(observer3);

            Assert.NotNull(commandSource1);
            Assert.NotNull(commandSource2);

            ICommand command1 = A.Fake<ICommand>();
            ICommand command2 = A.Fake<ICommand>();

            commandSource1.OnNext(command1);
            A.CallTo(() => observer1.OnNext(command1)).MustHaveHappenedOnceExactly();
            A.CallTo(() => observer2.OnNext(command1)).MustHaveHappenedOnceExactly();

            Exception exception = new Exception();
            commandSource1.OnError(exception);
            commandSource1.OnError(new Exception());
            commandSource1.OnError(new Exception());
            commandSource1.OnError(new Exception());
            A.CallTo(() => observer1.OnError(exception)).MustNotHaveHappened();
            A.CallTo(() => observer2.OnError(exception)).MustNotHaveHappened();

            commandSource2.OnNext(command2);
            A.CallTo(() => observer1.OnNext(command2)).MustHaveHappenedOnceExactly();
            A.CallTo(() => observer2.OnNext(command2)).MustHaveHappenedOnceExactly();

            A.CallTo(() => observer3.OnNext(A<Exception>._)).WhenArgumentsMatch(call => call.Get<Exception>(0).InnerException == exception).MustHaveHappenedOnceExactly();

            eventSub1.Dispose();
            eventSub2.Dispose();
            exceptionSub.Dispose();
        }

        [Fact]
        public void ShouldHandleUnderlyingRequestSubscriptionErrorsOnTheExceptionChannel()
        {
            IServiceEndpoint serviceEndpoint1 = A.Fake<IServiceEndpoint>();
            IServiceEndpoint serviceEndpoint2 = A.Fake<IServiceEndpoint>();
            IServiceEndpointClient serviceEndpointClient1 = A.Fake<IServiceEndpointClient>();
            IServiceEndpointClient serviceEndpointClient2 = A.Fake<IServiceEndpointClient>();

            IObservable<IRequest> observable1 = A.Fake<IObservable<IRequest>>();
            IObservable<IRequest> observable2 = A.Fake<IObservable<IRequest>>();

            IObserver<IRequest> observer1 = A.Fake<IObserver<IRequest>>();
            IObserver<IRequest> observer2 = A.Fake<IObserver<IRequest>>();
            IObserver<Exception> observer3 = A.Fake<IObserver<Exception>>();

            A.CallTo(() => serviceEndpoint1.Requests).Returns(observable1);
            A.CallTo(() => serviceEndpoint2.Requests).Returns(observable2);

            IObserver<IRequest> requestSource1 = null;
            IObserver<IRequest> requestSource2 = null;

            A.CallTo(() => observable1.Subscribe(A<IObserver<IRequest>>._)).Invokes(call => requestSource1 = call.GetArgument<IObserver<IRequest>>(0));
            A.CallTo(() => observable2.Subscribe(A<IObserver<IRequest>>._)).Invokes(call => requestSource2 = call.GetArgument<IObserver<IRequest>>(0));

            IServiceBus serviceBus = new ServiceBus(new[] { serviceEndpointClient1, serviceEndpointClient2 }, new[] { serviceEndpoint1, serviceEndpoint2 });

            IDisposable eventSub1 = serviceBus.Requests.Subscribe(observer1);
            IDisposable eventSub2 = serviceBus.Requests.Subscribe(observer2);
            IDisposable exceptionSub = serviceBus.Exceptions.Subscribe(observer3);

            Assert.NotNull(requestSource1);
            Assert.NotNull(requestSource2);

            IRequest request1 = A.Fake<IRequest>();
            IRequest request2 = A.Fake<IRequest>();

            requestSource1.OnNext(request1);
            A.CallTo(() => observer1.OnNext(request1)).MustHaveHappenedOnceExactly();
            A.CallTo(() => observer2.OnNext(request1)).MustHaveHappenedOnceExactly();

            Exception exception = new Exception();
            requestSource1.OnError(exception);
            requestSource1.OnError(new Exception());
            requestSource1.OnError(new Exception());
            requestSource1.OnError(new Exception());
            A.CallTo(() => observer1.OnError(exception)).MustNotHaveHappened();
            A.CallTo(() => observer2.OnError(exception)).MustNotHaveHappened();

            requestSource2.OnNext(request2);
            A.CallTo(() => observer1.OnNext(request2)).MustHaveHappenedOnceExactly();
            A.CallTo(() => observer2.OnNext(request2)).MustHaveHappenedOnceExactly();

            A.CallTo(() => observer3.OnNext(A<Exception>._)).WhenArgumentsMatch(call => call.Get<Exception>(0).InnerException == exception).MustHaveHappenedOnceExactly();

            eventSub1.Dispose();
            eventSub2.Dispose();
            exceptionSub.Dispose();
        }
        
        [Fact]
        public void ShouldSendCommandsToCorrectEndpoints()
        {
            IServiceEndpoint serviceEndpoint1 = A.Fake<IServiceEndpoint>();
            IServiceEndpoint serviceEndpoint2 = A.Fake<IServiceEndpoint>();
            IServiceEndpointClient serviceEndpointClient1 = A.Fake<IServiceEndpointClient>();
            IServiceEndpointClient serviceEndpointClient2 = A.Fake<IServiceEndpointClient>();
            IServiceEndpointClient serviceEndpointClient3 = A.Fake<IServiceEndpointClient>();

            ICommand command1 = A.Fake<ICommand>();
            ICommand command2 = A.Fake<ICommand>();
            ICommand command3 = A.Fake<ICommand>();

            A.CallTo(() => serviceEndpointClient1.CanHandle(A<ICommand>._)).Returns(true);
            A.CallTo(() => serviceEndpointClient2.CanHandle(A<ICommand>._)).Returns(false);
            A.CallTo(() => serviceEndpointClient3.CanHandle(A<ICommand>._)).Returns(true);

            IServiceBus serviceBus = new ServiceBus(new[] { serviceEndpointClient1, serviceEndpointClient2, serviceEndpointClient3 }, new[] { serviceEndpoint1, serviceEndpoint2 });
            
            serviceBus.SendAsync(command1);

            A.CallTo(() => serviceEndpointClient1.SendAsync(command1)).MustHaveHappenedOnceExactly();
            A.CallTo(() => serviceEndpointClient2.SendAsync(command1)).MustNotHaveHappened();
            A.CallTo(() => serviceEndpointClient3.SendAsync(command1)).MustHaveHappenedOnceExactly();
            
            serviceBus.SendAsync(new[]{command2, command3});

            A.CallTo(() => serviceEndpointClient1.SendAsync(command2)).MustHaveHappenedOnceExactly();
            A.CallTo(() => serviceEndpointClient2.SendAsync(command2)).MustNotHaveHappened();
            A.CallTo(() => serviceEndpointClient3.SendAsync(command2)).MustHaveHappenedOnceExactly();

            A.CallTo(() => serviceEndpointClient1.SendAsync(command3)).MustHaveHappenedOnceExactly();
            A.CallTo(() => serviceEndpointClient2.SendAsync(command3)).MustNotHaveHappened();
            A.CallTo(() => serviceEndpointClient3.SendAsync(command3)).MustHaveHappenedOnceExactly();
        } 
        
        [Fact]
        public void ShouldSendRequestsToCorrectEndpoints()
        {
            IServiceEndpoint serviceEndpoint1 = A.Fake<IServiceEndpoint>();
            IServiceEndpoint serviceEndpoint2 = A.Fake<IServiceEndpoint>();
            IServiceEndpointClient serviceEndpointClient1 = A.Fake<IServiceEndpointClient>();
            IServiceEndpointClient serviceEndpointClient2 = A.Fake<IServiceEndpointClient>();
            IServiceEndpointClient serviceEndpointClient3 = A.Fake<IServiceEndpointClient>();

            IRequest request = A.Fake<IRequest>();

            A.CallTo(() => serviceEndpointClient1.CanHandle(request)).Returns(true);
            A.CallTo(() => serviceEndpointClient2.CanHandle(request)).Returns(false);
            A.CallTo(() => serviceEndpointClient3.CanHandle(request)).Returns(true);

            IServiceBus serviceBus = new ServiceBus(new[] { serviceEndpointClient1, serviceEndpointClient2, serviceEndpointClient3 }, new[] { serviceEndpoint1, serviceEndpoint2 });
            
            IDisposable sub1 = serviceBus.GetResponses(request).Subscribe(A.Fake<IObserver<IResponse>>());

            A.CallTo(() => serviceEndpointClient1.GetResponses(request)).MustHaveHappenedOnceExactly();
            A.CallTo(() => serviceEndpointClient2.GetResponses(request)).MustNotHaveHappened();
            A.CallTo(() => serviceEndpointClient3.GetResponses(request)).MustHaveHappenedOnceExactly();

            IDisposable sub2 = serviceBus.GetResponses<IResponse>(request).Subscribe(A.Fake<IObserver<IResponse>>());

            A.CallTo(() => serviceEndpointClient1.GetResponses(request)).MustHaveHappenedTwiceExactly();
            A.CallTo(() => serviceEndpointClient2.GetResponses(request)).MustNotHaveHappened();
            A.CallTo(() => serviceEndpointClient3.GetResponses(request)).MustHaveHappenedTwiceExactly();

            sub1.Dispose();
            sub2.Dispose();
        }

        [Fact]
        public void ShouldCompleteGetResponseWhenOneResponseReturned()
        {
            IServiceEndpoint serviceEndpoint1 = A.Fake<IServiceEndpoint>();
            IServiceEndpointClient serviceEndpointClient1 = A.Fake<IServiceEndpointClient>();
            
            IRequest request = A.Fake<IRequest>();
            var response = A.Fake<IResponse>();
            var subject = new Subject<IResponse>();
            var requestCorrelationProvider = A.Fake<IRequestCorrelationProvider>();

            A.CallTo(() => requestCorrelationProvider.AreCorrelated(request, response)).Returns(true);
            A.CallTo(() => serviceEndpointClient1.CanHandle(request)).Returns(true);
            A.CallTo(() => serviceEndpointClient1.GetResponses(request)).Returns(subject);

            IServiceBus serviceBus = new ServiceBus(new[] { serviceEndpointClient1 }, new[] { serviceEndpoint1  }, requestCorrelationProvider);

            var observer = A.Fake<IObserver<IResponse>>();
            IDisposable sub1 = serviceBus.GetResponse<IResponse>(request).Subscribe(observer);

            A.CallTo(() => serviceEndpointClient1.GetResponses(request)).MustHaveHappenedOnceExactly();

            subject.OnNext(response);

            A.CallTo(() => observer.OnNext(response)).MustHaveHappenedOnceExactly();
            A.CallTo(() => observer.OnCompleted()).MustHaveHappenedOnceExactly();
            A.CallTo(() => observer.OnError(A<Exception>._)).MustNotHaveHappened();
            
            sub1.Dispose();
        }
        
        [Fact]
        public void ShouldPublishEventsToCorrectEndpoints()
        {
            IServiceEndpoint serviceEndpoint1 = A.Fake<IServiceEndpoint>();
            IServiceEndpoint serviceEndpoint2 = A.Fake<IServiceEndpoint>();
            IServiceEndpoint serviceEndpoint3 = A.Fake<IServiceEndpoint>();
            IServiceEndpointClient serviceEndpointClient1 = A.Fake<IServiceEndpointClient>();
            IServiceEndpointClient serviceEndpointClient2 = A.Fake<IServiceEndpointClient>();

            IEvent ev = A.Fake<IEvent>();

            A.CallTo(() => serviceEndpoint1.CanHandle(ev)).Returns(true);
            A.CallTo(() => serviceEndpoint2.CanHandle(ev)).Returns(false);
            A.CallTo(() => serviceEndpoint3.CanHandle(ev)).Returns(true);

            IServiceBus serviceBus = new ServiceBus(new[] { serviceEndpointClient1, serviceEndpointClient2 }, new[] { serviceEndpoint1, serviceEndpoint2, serviceEndpoint3 });
            
            serviceBus.PublishAsync(ev);

            A.CallTo(() => serviceEndpoint1.PublishAsync(ev)).MustHaveHappenedOnceExactly();
            A.CallTo(() => serviceEndpoint2.PublishAsync(ev)).MustNotHaveHappened();
            A.CallTo(() => serviceEndpoint3.PublishAsync(ev)).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public void ShouldSendResponsesToCorrectEndpoints()
        {
            IServiceEndpoint serviceEndpoint1 = A.Fake<IServiceEndpoint>();
            IServiceEndpoint serviceEndpoint2 = A.Fake<IServiceEndpoint>();
            IServiceEndpoint serviceEndpoint3 = A.Fake<IServiceEndpoint>();
            IServiceEndpointClient serviceEndpointClient1 = A.Fake<IServiceEndpointClient>();
            IServiceEndpointClient serviceEndpointClient2 = A.Fake<IServiceEndpointClient>();

            IRequest request = A.Fake<IRequest>();
            IResponse response = A.Fake<IResponse>();

            A.CallTo(() => serviceEndpoint1.CanHandle(response)).Returns(true);
            A.CallTo(() => serviceEndpoint2.CanHandle(response)).Returns(false);
            A.CallTo(() => serviceEndpoint3.CanHandle(response)).Returns(true);

            IServiceBus serviceBus = new ServiceBus(new[] { serviceEndpointClient1, serviceEndpointClient2 }, new[] { serviceEndpoint1, serviceEndpoint2, serviceEndpoint3 });

            serviceBus.ReplyAsync(request, response);

            A.CallTo(() => serviceEndpoint1.ReplyAsync(request, response)).MustHaveHappenedOnceExactly();
            A.CallTo(() => serviceEndpoint2.ReplyAsync(request, response)).MustNotHaveHappened();
            A.CallTo(() => serviceEndpoint3.ReplyAsync(request, response)).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public void ShouldSetRequestIdsOnRequestsWhenUsingDefaultRequestCorrelationProvider()
        {
            const string requestId = "MyOwnRequestId";

            IServiceEndpoint serviceEndpoint1 = A.Fake<IServiceEndpoint>();
            IServiceEndpoint serviceEndpoint2 = A.Fake<IServiceEndpoint>();
            IServiceEndpointClient serviceEndpointClient1 = A.Fake<IServiceEndpointClient>();
            IServiceEndpointClient serviceEndpointClient2 = A.Fake<IServiceEndpointClient>();

            IObservable<IResponse> observable1 = A.Fake<IObservable<IResponse>>();
            IObservable<IResponse> observable2 = A.Fake<IObservable<IResponse>>();

            IObserver<IResponse> observer1 = A.Fake<IObserver<IResponse>>();
            IObserver<IResponse> observer2 = A.Fake<IObserver<IResponse>>();

            IRequest request1 = A.Fake<IRequest>();
            IRequest request2 = A.Fake<IRequest>();

            request2.RequestId = requestId;

            A.CallTo(() => serviceEndpointClient1.CanHandle(request1)).Returns(true);
            A.CallTo(() => serviceEndpointClient2.CanHandle(request2)).Returns(true);

            A.CallTo(() => serviceEndpointClient1.GetResponses(request1)).Returns(observable1);
            A.CallTo(() => serviceEndpointClient2.GetResponses(request2)).Returns(observable2);

            IServiceBus serviceBus = new ServiceBus(new[] { serviceEndpointClient1, serviceEndpointClient2 }, new[] { serviceEndpoint1, serviceEndpoint2 });

            IObservable<IResponse> responses1 = serviceBus.GetResponses(request1);
            IObservable<IResponse> responses2 = serviceBus.GetResponses(request2);

            IDisposable sub1 = responses1.Subscribe(observer1);
            IDisposable sub2 = responses2.Subscribe(observer2);

            Assert.True(!string.IsNullOrEmpty(request1.RequestId), "RequestId not set on request");
            Assert.True(!string.IsNullOrEmpty(request1.RequesterId), "RequesterId not set on request");

            Assert.Equal(request2.RequestId, requestId); // Custom RequestId was overriden
            Assert.True(!string.IsNullOrEmpty(request2.RequesterId), "RequesterId not set on request");
            Assert.Equal(request1.RequesterId, request2.RequesterId); // RequesterId should the same on both requests

            sub1.Dispose();
            sub2.Dispose();
        }

        [Fact]
        public void ShouldAttemptToSendCommandToAllEndpointsWhenExceptionsAreThrown()
        {
            IServiceEndpoint serviceEndpoint1 = A.Fake<IServiceEndpoint>();
            IServiceEndpoint serviceEndpoint2 = A.Fake<IServiceEndpoint>();
            IServiceEndpointClient serviceEndpointClient1 = A.Fake<IServiceEndpointClient>();
            IServiceEndpointClient serviceEndpointClient2 = A.Fake<IServiceEndpointClient>();
            IServiceEndpointClient serviceEndpointClient3 = A.Fake<IServiceEndpointClient>();

            ICommand command = A.Fake<ICommand>();
            ICommand command2 = A.Fake<ICommand>();

            A.CallTo(() => serviceEndpointClient1.CanHandle(A<ICommand>._)).Returns(true);
            A.CallTo(() => serviceEndpointClient2.CanHandle(A<ICommand>._)).Returns(true);
            A.CallTo(() => serviceEndpointClient3.CanHandle(A<ICommand>._)).Returns(true);

            Exception originalException = new Exception("Something went wrong");
            A.CallTo(() => serviceEndpointClient1.SendAsync(A<ICommand>._)).Returns(Task.FromResult(true));
            A.CallTo(() => serviceEndpointClient2.SendAsync(A<ICommand>._)).Throws(originalException);
            A.CallTo(() => serviceEndpointClient3.SendAsync(A<ICommand>._)).Returns(Task.FromResult(true));

            IServiceBus serviceBus = new ServiceBus(new[] { serviceEndpointClient1, serviceEndpointClient2, serviceEndpointClient3 }, new[] { serviceEndpoint1, serviceEndpoint2 });

            AggregateException aggregateException = null;
            try
            {
                serviceBus.SendAsync(new[] {command, command2});
            }
            catch (AggregateException ex)
            {
                aggregateException = ex;
                _testOutputHelper.WriteLine(ex.ToString());
            }

            Assert.True(aggregateException != null, "No aggregate exception was thrown");
            Assert.True(aggregateException.InnerExceptions.Any(e => e.InnerException == originalException), "Aggregate exception did not contain original exception");

            A.CallTo(() => serviceEndpointClient1.SendAsync(command)).MustHaveHappenedOnceExactly();
            A.CallTo(() => serviceEndpointClient2.SendAsync(command)).MustHaveHappenedOnceExactly();
            A.CallTo(() => serviceEndpointClient3.SendAsync(command)).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public void ShouldAttemptToPublishEventToAllEndpointsWhenExceptionsAreThrown()
        {
            IServiceEndpoint serviceEndpoint1 = A.Fake<IServiceEndpoint>();
            IServiceEndpoint serviceEndpoint2 = A.Fake<IServiceEndpoint>();
            IServiceEndpoint serviceEndpoint3 = A.Fake<IServiceEndpoint>();
            IServiceEndpointClient serviceEndpointClient1 = A.Fake<IServiceEndpointClient>();
            IServiceEndpointClient serviceEndpointClient2 = A.Fake<IServiceEndpointClient>();

            IEvent ev = A.Fake<IEvent>();

            A.CallTo(() => serviceEndpoint1.CanHandle(A<IEvent>._)).Returns(true);
            A.CallTo(() => serviceEndpoint2.CanHandle(A<IEvent>._)).Returns(true);
            A.CallTo(() => serviceEndpoint3.CanHandle(A<IEvent>._)).Returns(true);

            Exception originalException = new Exception("Something went wrong");
            A.CallTo(() => serviceEndpoint1.PublishAsync(ev)).Returns(Task.FromResult(true));
            A.CallTo(() => serviceEndpoint2.PublishAsync(ev)).Throws(originalException);
            A.CallTo(() => serviceEndpoint3.PublishAsync(ev)).Returns(Task.FromResult(true));

            IServiceBus serviceBus = new ServiceBus(new[] { serviceEndpointClient1, serviceEndpointClient2 }, new[] { serviceEndpoint1, serviceEndpoint2, serviceEndpoint3 });

            AggregateException aggregateException = null;
            try
            {
                serviceBus.PublishAsync(ev);
            }
            catch (AggregateException ex)
            {
                aggregateException = ex;
                _testOutputHelper.WriteLine(ex.ToString());
            }

            Assert.True(aggregateException != null, "No aggregate exception was thrown");
            Assert.True(aggregateException.InnerExceptions.Any(e => e.InnerException == originalException), "Aggregate exception did not contain original exception");

            A.CallTo(() => serviceEndpoint1.PublishAsync(ev)).MustHaveHappenedOnceExactly();
            A.CallTo(() => serviceEndpoint2.PublishAsync(ev)).MustHaveHappenedOnceExactly();
            A.CallTo(() => serviceEndpoint3.PublishAsync(ev)).MustHaveHappenedOnceExactly();
        }
        
        [Fact]
        public void ShouldAttemptToSendResponseToAllEndpointsWhenExceptionsAreThrown()
        {
            IServiceEndpoint serviceEndpoint1 = A.Fake<IServiceEndpoint>();
            IServiceEndpoint serviceEndpoint2 = A.Fake<IServiceEndpoint>();
            IServiceEndpoint serviceEndpoint3 = A.Fake<IServiceEndpoint>();
            IServiceEndpointClient serviceEndpointClient1 = A.Fake<IServiceEndpointClient>();
            IServiceEndpointClient serviceEndpointClient2 = A.Fake<IServiceEndpointClient>();

            IRequest request = A.Fake<IRequest>();
            IResponse response = A.Fake<IResponse>();

            A.CallTo(() => serviceEndpoint1.CanHandle(A<IResponse>._)).Returns(true);
            A.CallTo(() => serviceEndpoint2.CanHandle(A<IResponse>._)).Returns(true);
            A.CallTo(() => serviceEndpoint3.CanHandle(A<IResponse>._)).Returns(true);

            Exception originalException = new Exception("Something went wrong");
            A.CallTo(() => serviceEndpoint2.ReplyAsync(request, response)).Throws(originalException);

            IServiceBus serviceBus = new ServiceBus(new[] { serviceEndpointClient1, serviceEndpointClient2 }, new[] { serviceEndpoint1, serviceEndpoint2, serviceEndpoint3 });

            AggregateException aggregateException = null;
            
            try
            {
                serviceBus.ReplyAsync(request, response);
            }
            catch (AggregateException ex)
            {
                aggregateException = ex;
                _testOutputHelper.WriteLine(ex.ToString());
            }

            Assert.True(aggregateException != null, "No aggregate exception was thrown");
            Assert.True(aggregateException.InnerExceptions.Any(e => e.InnerException == originalException), "Aggregate exception did not contain original exception");

            A.CallTo(() => serviceEndpoint1.ReplyAsync(request, response)).MustHaveHappenedOnceExactly();
            A.CallTo(() => serviceEndpoint2.ReplyAsync(request, response)).MustHaveHappenedOnceExactly();
            A.CallTo(() => serviceEndpoint3.ReplyAsync(request, response)).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public void ShouldDisposeEndpointsWhenDisposed()
        {
            IServiceEndpoint serviceEndpoint1 = A.Fake<IServiceEndpoint>();
            IServiceEndpoint serviceEndpoint2 = A.Fake<IServiceEndpoint>();
            IServiceEndpointClient serviceEndpointClient1 = A.Fake<IServiceEndpointClient>();
            IServiceEndpointClient serviceEndpointClient2 = A.Fake<IServiceEndpointClient>();

            IServiceBus serviceBus = new ServiceBus(new[] { serviceEndpointClient1, serviceEndpointClient2 }, new[] { serviceEndpoint1, serviceEndpoint2 });

            ((IDisposable)serviceBus).Dispose();

            A.CallTo(() => serviceEndpoint1.Dispose()).MustHaveHappenedOnceExactly();
            A.CallTo(() => serviceEndpoint2.Dispose()).MustHaveHappenedOnceExactly();
            A.CallTo(() => serviceEndpointClient1.Dispose()).MustHaveHappenedOnceExactly();
            A.CallTo(() => serviceEndpointClient2.Dispose()).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public void ShouldCatchAndHandleExceptionsThrownByEndpointObservables()
        {
            FakeServiceEndpoint erroringEndpoint = new FakeServiceEndpoint(typeof(ITestServiceMessage1)) { ThrowException = true };
            FakeServiceEndpoint serviceEndpoint = new FakeServiceEndpoint(typeof(ITestServiceMessage2));

            IServiceBus serviceBus = ServiceBus.Configure()
                .WithEndpoint((IServiceEndpointClient) erroringEndpoint)
                .WithEndpoint((IServiceEndpoint)erroringEndpoint)
                .WithEndpoint((IServiceEndpointClient) serviceEndpoint)
                .WithEndpoint((IServiceEndpoint)serviceEndpoint)
                .UsingConsoleLogging()
                .Create();

            ConcurrentBag<IMessage> messages = new ConcurrentBag<IMessage>();
            ConcurrentBag<Exception> exceptions = new ConcurrentBag<Exception>();

            serviceBus.Events.Subscribe(messages.Add);
            serviceBus.Commands.Subscribe(messages.Add);
            serviceBus.Requests.Subscribe(messages.Add);
            serviceBus.Exceptions.Subscribe(exceptions.Add);

            // trigger exception
            serviceBus.PublishAsync(new TestServiceEvent1());

            TestServiceEvent2 message1 = new TestServiceEvent2();
            serviceBus.PublishAsync(message1);

            // trigger another exception
            serviceBus.PublishAsync(new TestServiceEvent1());

            TestServiceEvent2 message2 = new TestServiceEvent2();
            serviceBus.PublishAsync(message2);

            Assert.Equal(exceptions.Count, 2);
            Assert.True(messages.Contains(message1), "message1 not received");
            Assert.True(messages.Contains(message2), "message2 not received");
        }

        [Fact]
        public void ShouldDeliverAllMessagesFromEndpointsWithoutLoggingEnabled()
        {
            FakeServiceEndpoint serviceEndpoint1 = new FakeServiceEndpoint(typeof(ITestServiceMessage1));
            FakeServiceEndpoint serviceEndpoint2 = new FakeServiceEndpoint(typeof(ITestServiceMessage2));

            IServiceBus serviceBus = ServiceBus.Configure()
                .WithEndpoint((IServiceEndpointClient)serviceEndpoint1)
                .WithEndpoint((IServiceEndpoint)serviceEndpoint1)
                .WithEndpoint((IServiceEndpointClient)serviceEndpoint2)
                .WithEndpoint((IServiceEndpoint)serviceEndpoint2)
                .UsingConsoleLogging(endpoint => false)
                .Create();

            List<Exception> exceptions = new List<Exception>();
            List<IMessage> messages = new List<IMessage>();
            serviceBus.Exceptions.Subscribe(exceptions.Add);
            serviceBus.Events.Subscribe(messages.Add);
            serviceBus.Commands.Subscribe(messages.Add);
            
            serviceEndpoint1.Messages.OnNext(new TestServiceEvent1());
            serviceEndpoint1.Messages.OnNext(new TestServiceCommand1());

            Assert.Equal(exceptions.Count, 0);
            Assert.Equal(messages.Count, 2);
            Assert.Equal(messages[0].GetType(), typeof(TestServiceEvent1));
            Assert.Equal(messages[1].GetType(), typeof(TestServiceCommand1));
            
        }

        [Fact]
        public void ShouldPublishMessagesToLocalBusWhenConfigured()
        {
            FakeServiceEndpoint fakeServiceEndpoint = new FakeServiceEndpoint(typeof(ITestServiceMessage1));
            FakeServiceEndpoint fakeServer = new FakeServiceEndpoint(typeof(ITestServiceMessage2));

            var localBus = new SubjectServiceBus();

            IServiceBus serviceBus = ServiceBus.Configure()
                .WithEndpoint((IServiceEndpoint)fakeServiceEndpoint)
                .WithEndpoint((IServiceEndpointClient)fakeServer)
                .PublishLocally(localBus).AnyMessagesWithNoEndpointClients()
                .Create();

            List<Exception> exceptions = new List<Exception>();
            List<IMessage> serviceBusMessages = new List<IMessage>();
            List<IMessage> localBusMessages = new List<IMessage>();
            serviceBus.Exceptions.Subscribe(exceptions.Add);
            serviceBus.Events.Subscribe(serviceBusMessages.Add);
            serviceBus.Commands.Subscribe(serviceBusMessages.Add);
            serviceBus.Requests.Subscribe(serviceBusMessages.Add);
            serviceBus.Exceptions.Subscribe(exceptions.Add);
            localBus.Events.Subscribe(localBusMessages.Add);
            localBus.Commands.Subscribe(localBusMessages.Add);
            localBus.Requests.Subscribe(localBusMessages.Add);

            fakeServer.Commands.Subscribe(command => fakeServer.PublishAsync(new TestServiceEvent2()));
            fakeServer.Requests.Subscribe(request => fakeServer.ReplyAsync(request, new TestServiceResponse2()));
            serviceBus.Requests.OfType<TestServiceRequest1>().Subscribe(request =>
            {
                serviceBus.ReplyAsync(request, new TestServiceResponse1());
            });

            serviceBus.GetResponses(new TestServiceRequest1()).Subscribe(serviceBusMessages.Add);
            serviceBus.GetResponses(new TestServiceRequest2()).Subscribe(serviceBusMessages.Add);
            serviceBus.SendAsync(new TestServiceCommand2());
            serviceBus.PublishAsync(new TestServiceEvent1());
            serviceBus.SendAsync(new TestServiceCommand1());
            serviceBus.PublishAsync(new TestEventBelongingToNoService());

            Assert.Equal(exceptions.Count, 0);

            Assert.Equal(4, localBusMessages.Count);
            Assert.Equal(localBusMessages[0].GetType(), typeof(TestServiceRequest1));
            Assert.Equal(localBusMessages[1].GetType(), typeof(TestServiceEvent1));
            Assert.Equal(localBusMessages[2].GetType(), typeof(TestServiceCommand1));
            Assert.Equal(localBusMessages[3].GetType(), typeof(TestEventBelongingToNoService));

            Assert.Equal(7, serviceBusMessages.Count);
            Assert.Equal(serviceBusMessages[0].GetType(), typeof(TestServiceRequest1)); // locally published
            Assert.Equal(serviceBusMessages[1].GetType(), typeof(TestServiceResponse1)); // locally published
            Assert.Equal(serviceBusMessages[2].GetType(), typeof(TestServiceResponse2));
            Assert.Equal(serviceBusMessages[3].GetType(), typeof(TestServiceEvent2));
            Assert.Equal(serviceBusMessages[4].GetType(), typeof(TestServiceEvent1)); // locally published
            Assert.Equal(serviceBusMessages[5].GetType(), typeof(TestServiceCommand1)); // locally published
            Assert.Equal(serviceBusMessages[6].GetType(), typeof(TestEventBelongingToNoService)); // locally published
            
        }
        
        [Fact]
        public void ShouldPublishMessagesWithNoEndpointToLocalBusWhenConfigured()
        {
            FakeServiceEndpoint fakeServiceEndpoint = new FakeServiceEndpoint(typeof(ITestServiceMessage1));
            FakeServiceEndpoint fakeServer = new FakeServiceEndpoint(typeof(ITestServiceMessage2));

            var localBus = new SubjectServiceBus();

            var testScheduler = new TestScheduler();

            IServiceBus serviceBus = ServiceBus.Configure()
                .WithEndpoint((IServiceEndpoint)fakeServiceEndpoint)
                .WithEndpoint((IServiceEndpointClient)fakeServer)
                .PublishLocally(localBus).OnlyMessagesWithNoEndpoints()
                .UsingConsoleMonitor(TimeSpan.FromSeconds(5), testScheduler)
                .UsingConsoleLogging()
                .Create();

            List<Exception> exceptions = new List<Exception>();
            List<IMessage> serviceBusMessages = new List<IMessage>();
            List<IMessage> localBusMessages = new List<IMessage>();
            serviceBus.Exceptions.Subscribe(exceptions.Add);
            serviceBus.Events.Subscribe(serviceBusMessages.Add);
            serviceBus.Commands.Subscribe(serviceBusMessages.Add);
            serviceBus.Requests.Subscribe(serviceBusMessages.Add);
            localBus.Events.Subscribe(localBusMessages.Add);
            localBus.Commands.Subscribe(localBusMessages.Add);
            localBus.Requests.Subscribe(localBusMessages.Add);

            fakeServer.Commands.Subscribe(command => fakeServer.PublishAsync(new TestServiceEvent2()));
            fakeServer.Requests.Subscribe(request => fakeServer.ReplyAsync(request, new TestServiceResponse2()));
            serviceBus.Requests.OfType<TestServiceRequest1>().Subscribe(request =>
            {
                serviceBus.ReplyAsync(request, new TestServiceResponse1());
            });

            serviceBus.GetResponses(new TestServiceRequest1()).Subscribe(serviceBusMessages.Add);
            serviceBus.GetResponses(new TestServiceRequest2()).Subscribe(serviceBusMessages.Add);
            serviceBus.SendAsync(new TestServiceCommand2());
            serviceBus.PublishAsync(new TestServiceEvent1());
            serviceBus.PublishAsync(new TestEventBelongingToNoService());

            Assert.Equal(exceptions.Count, 0);

            Assert.Equal(localBusMessages.Count, 1);
            Assert.Equal(localBusMessages[0].GetType(), typeof(TestEventBelongingToNoService));

            Assert.Equal(serviceBusMessages.Count, 3);
            Assert.Equal(serviceBusMessages[0].GetType(), typeof(TestServiceResponse2));
            Assert.Equal(serviceBusMessages[1].GetType(), typeof(TestServiceEvent2));
            Assert.Equal(serviceBusMessages[2].GetType(), typeof(TestEventBelongingToNoService)); // locally published

            testScheduler.AdvanceBy(TimeSpan.FromSeconds(10).Ticks);
            
        }

        [Fact]
        public async Task ShouldMonitorAllMessagesSentAndReceived()
        {
            FakeServiceEndpoint fakeServiceEndpoint = new FakeServiceEndpoint(typeof(ITestServiceMessage1));
            FakeServiceEndpoint fakeServer = new FakeServiceEndpoint(typeof(ITestServiceMessage2));

            List<IMessage> monitorReceived = new List<IMessage>();
            List<IMessage> monitorSent = new List<IMessage>();

            var monitorFactory = A.Fake<IMonitorFactory<IMessage>>();
            var monitor = A.Fake<IMonitor<IMessage>>();
            A.CallTo(() => monitor.MessageReceived(A<IMessage>._, A<TimeSpan>._)).Invokes(call =>
            {
                var message = call.GetArgument<IMessage>(0);
                _testOutputHelper.WriteLine("Received {0}", message);
                monitorReceived.Add(message);
            });
            A.CallTo(() => monitor.MessageSent(A<IMessage>._, A<TimeSpan>._)).Invokes(call =>
            {
                var message = call.GetArgument<IMessage>(0);
                _testOutputHelper.WriteLine("Sent {0}", message);
                monitorSent.Add(message);
            });
            A.CallTo(() => monitorFactory.Create(A<string>._)).Returns(monitor);

            IServiceBus serviceBus = ServiceBus.Configure()
                .WithEndpoint((IServiceEndpoint)fakeServiceEndpoint)
                .WithEndpoint((IServiceEndpointClient)fakeServer)
                .UsingMonitor(monitorFactory)
                .Create();

            List<Exception> exceptions = new List<Exception>();
            List<IMessage> serviceBusMessages = new List<IMessage>();
            serviceBus.Exceptions.Subscribe(exceptions.Add);
            serviceBus.Events.Subscribe(serviceBusMessages.Add);
            serviceBus.Commands.Subscribe(serviceBusMessages.Add);
            serviceBus.Requests.Subscribe(serviceBusMessages.Add);

            fakeServer.Commands.OfType<TestServiceCommand1>().Subscribe(async command => await fakeServer.PublishAsync(new TestServiceEvent2()));
            fakeServer.Requests.OfType<TestServiceRequest2>().Subscribe(async request => await fakeServer.ReplyAsync(request, new TestServiceResponse2()));

            serviceBus.GetResponses(new TestServiceRequest2()).Subscribe(serviceBusMessages.Add);
            await serviceBus.SendAsync(new TestServiceCommand2());
            await serviceBus.PublishAsync(new TestServiceEvent1());

            Assert.Equal(monitorSent.Count, 3);
            Assert.Equal(monitorSent.OfType<TestServiceCommand2>().Count(), 1);
            Assert.Equal(monitorSent.OfType<TestServiceEvent1>().Count(), 1);
            Assert.Equal(monitorSent.OfType<TestServiceRequest2>().Count(), 1);

            Assert.Equal(monitorReceived.Count, 1);
            Assert.Equal(monitorReceived.OfType<TestServiceResponse2>().Count(), 1);
        }
    }
    
    public interface ITestServiceMessage1 : IMessage {}
    public interface ITestServiceMessage2 : IMessage {}
    public class TestServiceEvent1 : ITestServiceMessage1, IEvent { }
    public class TestEventBelongingToNoService : IEvent { }
    public class TestServiceEvent2 : TestServiceEventBase { }
    public class TestServiceCommand1 : ITestServiceMessage1, ICommand { }
    public class TestServiceCommand2 : TestServiceCommandBase { }
    public class TestServiceCommandBase : ITestServiceMessage2, ICommand { }
    public class TestServiceEventBase : ITestServiceMessage2, IEvent { }
    public class TestServiceRequest1 : ITestServiceMessage1, IRequest 
    {
        public string RequestId { get; set; }
        public string RequesterId { get; set; }
    }
    public class TestServiceRequest2 : ITestServiceMessage2, IRequest 
    {
        public string RequestId { get; set; }
        public string RequesterId { get; set; }
    }

    public class TestServiceResponse1 : ITestServiceMessage1, IResponse 
    {
        public string RequestId { get; set; }
        public string RequesterId { get; set; }
    }

    public class TestServiceResponse2 : ITestServiceMessage2, IResponse 
    {
        public string RequestId { get; set; }
        public string RequesterId { get; set; }
    }
}