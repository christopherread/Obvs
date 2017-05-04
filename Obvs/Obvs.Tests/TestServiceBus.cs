using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using FakeItEasy;
using NUnit.Framework;
using Obvs.Types;

namespace Obvs.Tests
{
    [TestFixture]
    public class TestServiceBus
    {
        [Test]
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

            IServiceBus serviceBus = new ServiceBus(new[] { serviceEndpointClient1, serviceEndpointClient2 }, new[] { serviceEndpoint1, serviceEndpoint2 });

            IDisposable sub1 = serviceBus.Requests.Subscribe(observer1);
            IDisposable sub2 = serviceBus.Requests.Subscribe(observer2);
            
            A.CallTo(() => serviceEndpoint1.Requests).MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => serviceEndpoint2.Requests).MustHaveHappened(Repeated.Exactly.Once);

            A.CallTo(() => observable1.Subscribe(A<IObserver<IRequest>>._)).MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => observable2.Subscribe(A<IObserver<IRequest>>._)).MustHaveHappened(Repeated.Exactly.Once);

            sub1.Dispose();
            sub2.Dispose();
        }
        
        [Test]
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
            
            A.CallTo(() => serviceEndpoint1.Commands).MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => serviceEndpoint2.Commands).MustHaveHappened(Repeated.Exactly.Once);

            A.CallTo(() => observable1.Subscribe(A<IObserver<ICommand>>._)).MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => observable2.Subscribe(A<IObserver<ICommand>>._)).MustHaveHappened(Repeated.Exactly.Once);

            sub1.Dispose();
            sub2.Dispose();
        }
        
        [Test]
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
            
            //A.CallTo(() => serviceEndpointClient1.Events).MustHaveHappened(Repeated.Exactly.Once);
            //A.CallTo(() => serviceEndpointClient2.Events).MustHaveHappened(Repeated.Exactly.Once);

            A.CallTo(() => observable1.Subscribe(A<IObserver<IEvent>>._)).MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => observable2.Subscribe(A<IObserver<IEvent>>._)).MustHaveHappened(Repeated.Exactly.Once);

            sub1.Dispose();
            sub2.Dispose();
        }
        
        [Test]
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

            A.CallTo(() => serviceEndpointClient1.GetResponses(request)).MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => serviceEndpointClient2.GetResponses(request)).MustHaveHappened(Repeated.Exactly.Once);

            A.CallTo(() => observable1.Subscribe(A<IObserver<IResponse>>._)).MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => observable2.Subscribe(A<IObserver<IResponse>>._)).MustHaveHappened(Repeated.Exactly.Once);

            sub1.Dispose();
            sub2.Dispose();
        }

        [Test]
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

            Assert.That(internalObserver1, Is.Not.Null);
            Assert.That(internalObserver2, Is.Not.Null);

            IRequest request1 = A.Fake<IRequest>();
            IRequest request2 = A.Fake<IRequest>();
            internalObserver1.OnNext(request1);
            internalObserver2.OnNext(request2);

            A.CallTo(() => observer1.OnNext(request1)).MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => observer2.OnNext(request1)).MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => observer1.OnNext(request2)).MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => observer2.OnNext(request2)).MustHaveHappened(Repeated.Exactly.Once);

            sub1.Dispose();
            sub2.Dispose();
        }
        
        [Test]
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

            Assert.That(internalObserver1, Is.Not.Null);
            Assert.That(internalObserver2, Is.Not.Null);

            ICommand command1 = A.Fake<ICommand>();
            ICommand command2 = A.Fake<ICommand>();
            internalObserver1.OnNext(command1);
            internalObserver2.OnNext(command2);

            A.CallTo(() => observer1.OnNext(command1)).MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => observer2.OnNext(command1)).MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => observer1.OnNext(command2)).MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => observer2.OnNext(command2)).MustHaveHappened(Repeated.Exactly.Once);

            sub1.Dispose();
            sub2.Dispose();
        }
        
        [Test]
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

            Assert.That(internalObserver1, Is.Not.Null);
            Assert.That(internalObserver2, Is.Not.Null);

            IEvent event1 = A.Fake<IEvent>();
            IEvent event2 = A.Fake<IEvent>();
            internalObserver1.OnNext(event1);
            internalObserver2.OnNext(event2);

            A.CallTo(() => observer1.OnNext(event1)).MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => observer2.OnNext(event1)).MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => observer1.OnNext(event2)).MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => observer2.OnNext(event2)).MustHaveHappened(Repeated.Exactly.Once);

            sub1.Dispose();
            sub2.Dispose();
        }
        
        [Test]
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

            Assert.That(internalObserver1, Is.Not.Null);
            Assert.That(internalObserver2, Is.Not.Null);

            // ensure id's on responses match those set on the request
            IResponse response1 = A.Fake<IResponse>();
            IResponse response2 = A.Fake<IResponse>();
            response1.RequestId = request.RequestId;
            response2.RequestId = request.RequestId;
            response1.RequesterId = request.RequesterId;
            response2.RequesterId = request.RequesterId;

            internalObserver1.OnNext(response1);
            internalObserver2.OnNext(response2);

            A.CallTo(() => observer1.OnNext(response1)).MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => observer2.OnNext(response1)).MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => observer1.OnNext(response1)).MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => observer2.OnNext(response2)).MustHaveHappened(Repeated.Exactly.Once);

            sub1.Dispose();
            sub2.Dispose();
        }

        [Test]
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

            Assert.That(requestSource1, Is.Not.Null);
            Assert.That(requestSource2, Is.Not.Null);

            IRequest request = A.Fake<IRequest>();

            requestSource1.OnNext(request);
            A.CallTo(() => observer1.OnNext(request)).MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => observer2.OnNext(request)).MustHaveHappened(Repeated.Exactly.Once);

            // dispose of first subscriptions
            requestSub1.Dispose();

            // second subscription should still be active
            requestSource1.OnNext(request);
            A.CallTo(() => observer1.OnNext(request)).MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => observer2.OnNext(request)).MustHaveHappened(Repeated.Exactly.Twice);

            // dispose of second subscriptions
            requestSub2.Dispose();
            
            // no subscriptions should be active
            requestSource1.OnNext(request);
            A.CallTo(() => observer1.OnNext(request)).MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => observer2.OnNext(request)).MustHaveHappened(Repeated.Exactly.Twice);
        }

        [Test]
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

            Assert.That(commandSource1, Is.Not.Null);
            Assert.That(commandSource2, Is.Not.Null);

            ICommand command = A.Fake<ICommand>();

            commandSource1.OnNext(command);
            A.CallTo(() => observer1.OnNext(command)).MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => observer2.OnNext(command)).MustHaveHappened(Repeated.Exactly.Once);

            // dispose of first subscriptions
            commandSub1.Dispose();

            // second subscription should still be active
            commandSource1.OnNext(command);
            A.CallTo(() => observer1.OnNext(command)).MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => observer2.OnNext(command)).MustHaveHappened(Repeated.Exactly.Twice);

            // dispose of second subscriptions
            commandSub2.Dispose();
            
            // no subscriptions should be active
            commandSource1.OnNext(command);
            A.CallTo(() => observer1.OnNext(command)).MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => observer2.OnNext(command)).MustHaveHappened(Repeated.Exactly.Twice);
        }

        [Test]
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

            Assert.That(eventSource1, Is.Not.Null);
            Assert.That(eventSource2, Is.Not.Null);

            IEvent ev = A.Fake<IEvent>();

            eventSource1.OnNext(ev);
            A.CallTo(() => observer1.OnNext(ev)).MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => observer2.OnNext(ev)).MustHaveHappened(Repeated.Exactly.Once);

            // dispose of first subscriptions
            eventSub1.Dispose();

            // second subscription should still be active
            eventSource1.OnNext(ev);
            A.CallTo(() => observer1.OnNext(ev)).MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => observer2.OnNext(ev)).MustHaveHappened(Repeated.Exactly.Twice);

            // dispose of second subscriptions
            eventSub2.Dispose();
            
            // no subscriptions should be active
            eventSource1.OnNext(ev);
            A.CallTo(() => observer1.OnNext(ev)).MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => observer2.OnNext(ev)).MustHaveHappened(Repeated.Exactly.Twice);
        }

        [Test]
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

            Assert.That(eventSource1, Is.Not.Null);
            Assert.That(eventSource2, Is.Not.Null);

            IEvent event1 = A.Fake<IEvent>();
            IEvent event2 = A.Fake<IEvent>();

            eventSource1.OnNext(event1);
            A.CallTo(() => observer1.OnNext(event1)).MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => observer2.OnNext(event1)).MustHaveHappened(Repeated.Exactly.Once);

            Exception exception = new Exception();
            eventSource1.OnError(exception);
            eventSource1.OnError(new Exception());
            eventSource1.OnError(new Exception());
            eventSource1.OnError(new Exception());
            A.CallTo(() => observer1.OnError(exception)).MustNotHaveHappened();
            A.CallTo(() => observer2.OnError(exception)).MustNotHaveHappened();

            eventSource2.OnNext(event2);
            A.CallTo(() => observer1.OnNext(event2)).MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => observer2.OnNext(event2)).MustHaveHappened(Repeated.Exactly.Once);

            A.CallTo(() => observer3.OnNext(A<Exception>._)).WhenArgumentsMatch(call => call.Get<Exception>(0).InnerException == exception).MustHaveHappened(Repeated.Exactly.Once);

            eventSub1.Dispose();
            eventSub2.Dispose();
            exceptionSub.Dispose();
        }

        [Test]
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

            Assert.That(commandSource1, Is.Not.Null);
            Assert.That(commandSource2, Is.Not.Null);

            ICommand command1 = A.Fake<ICommand>();
            ICommand command2 = A.Fake<ICommand>();

            commandSource1.OnNext(command1);
            A.CallTo(() => observer1.OnNext(command1)).MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => observer2.OnNext(command1)).MustHaveHappened(Repeated.Exactly.Once);

            Exception exception = new Exception();
            commandSource1.OnError(exception);
            commandSource1.OnError(new Exception());
            commandSource1.OnError(new Exception());
            commandSource1.OnError(new Exception());
            A.CallTo(() => observer1.OnError(exception)).MustNotHaveHappened();
            A.CallTo(() => observer2.OnError(exception)).MustNotHaveHappened();

            commandSource2.OnNext(command2);
            A.CallTo(() => observer1.OnNext(command2)).MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => observer2.OnNext(command2)).MustHaveHappened(Repeated.Exactly.Once);

            A.CallTo(() => observer3.OnNext(A<Exception>._)).WhenArgumentsMatch(call => call.Get<Exception>(0).InnerException == exception).MustHaveHappened(Repeated.Exactly.Once);

            eventSub1.Dispose();
            eventSub2.Dispose();
            exceptionSub.Dispose();
        }

        [Test]
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

            Assert.That(requestSource1, Is.Not.Null);
            Assert.That(requestSource2, Is.Not.Null);

            IRequest request1 = A.Fake<IRequest>();
            IRequest request2 = A.Fake<IRequest>();

            requestSource1.OnNext(request1);
            A.CallTo(() => observer1.OnNext(request1)).MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => observer2.OnNext(request1)).MustHaveHappened(Repeated.Exactly.Once);

            Exception exception = new Exception();
            requestSource1.OnError(exception);
            requestSource1.OnError(new Exception());
            requestSource1.OnError(new Exception());
            requestSource1.OnError(new Exception());
            A.CallTo(() => observer1.OnError(exception)).MustNotHaveHappened();
            A.CallTo(() => observer2.OnError(exception)).MustNotHaveHappened();

            requestSource2.OnNext(request2);
            A.CallTo(() => observer1.OnNext(request2)).MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => observer2.OnNext(request2)).MustHaveHappened(Repeated.Exactly.Once);

            A.CallTo(() => observer3.OnNext(A<Exception>._)).WhenArgumentsMatch(call => call.Get<Exception>(0).InnerException == exception).MustHaveHappened(Repeated.Exactly.Once);

            eventSub1.Dispose();
            eventSub2.Dispose();
            exceptionSub.Dispose();
        }
        
        [Test]
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
            
            serviceBus.Send(command1);

            A.CallTo(() => serviceEndpointClient1.Send(command1)).MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => serviceEndpointClient2.Send(command1)).MustNotHaveHappened();
            A.CallTo(() => serviceEndpointClient3.Send(command1)).MustHaveHappened(Repeated.Exactly.Once);
            
            serviceBus.Send(new[]{command2, command3});

            A.CallTo(() => serviceEndpointClient1.Send(command2)).MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => serviceEndpointClient2.Send(command2)).MustNotHaveHappened();
            A.CallTo(() => serviceEndpointClient3.Send(command2)).MustHaveHappened(Repeated.Exactly.Once);

            A.CallTo(() => serviceEndpointClient1.Send(command3)).MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => serviceEndpointClient2.Send(command3)).MustNotHaveHappened();
            A.CallTo(() => serviceEndpointClient3.Send(command3)).MustHaveHappened(Repeated.Exactly.Once);
        } 
        
        [Test]
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

            A.CallTo(() => serviceEndpointClient1.GetResponses(request)).MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => serviceEndpointClient2.GetResponses(request)).MustNotHaveHappened();
            A.CallTo(() => serviceEndpointClient3.GetResponses(request)).MustHaveHappened(Repeated.Exactly.Once);

            IDisposable sub2 = serviceBus.GetResponses<IResponse>(request).Subscribe(A.Fake<IObserver<IResponse>>());

            A.CallTo(() => serviceEndpointClient1.GetResponses(request)).MustHaveHappened(Repeated.Exactly.Twice);
            A.CallTo(() => serviceEndpointClient2.GetResponses(request)).MustNotHaveHappened();
            A.CallTo(() => serviceEndpointClient3.GetResponses(request)).MustHaveHappened(Repeated.Exactly.Twice);

            sub1.Dispose();
            sub2.Dispose();
        }
        
        [Test]
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
            
            serviceBus.Publish(ev);

            A.CallTo(() => serviceEndpoint1.Publish(ev)).MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => serviceEndpoint2.Publish(ev)).MustNotHaveHappened();
            A.CallTo(() => serviceEndpoint3.Publish(ev)).MustHaveHappened(Repeated.Exactly.Once);
        }

        [Test]
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

            serviceBus.Reply(request, response);

            A.CallTo(() => serviceEndpoint1.Reply(request, response)).MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => serviceEndpoint2.Reply(request, response)).MustNotHaveHappened();
            A.CallTo(() => serviceEndpoint3.Reply(request, response)).MustHaveHappened(Repeated.Exactly.Once);
        }

        [Test]
        public void ShouldSetRequestIdsOnRequests()
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

            Assert.That(!string.IsNullOrEmpty(request1.RequestId), "RequestId not set on request");
            Assert.That(!string.IsNullOrEmpty(request1.RequesterId), "RequesterId not set on request");

            Assert.That(request2.RequestId, Is.EqualTo(requestId), "Custom RequestId was overriden");
            Assert.That(!string.IsNullOrEmpty(request2.RequesterId), "RequesterId not set on request");
            Assert.That(request1.RequesterId, Is.EqualTo(request2.RequesterId), "RequesterId should the same on both requests");

            sub1.Dispose();
            sub2.Dispose();
        }

        [Test]
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
            A.CallTo(() => serviceEndpointClient2.Send(A<ICommand>._)).Throws(originalException);

            IServiceBus serviceBus = new ServiceBus(new[] { serviceEndpointClient1, serviceEndpointClient2, serviceEndpointClient3 }, new[] { serviceEndpoint1, serviceEndpoint2 });

            AggregateException aggregateException = null;
            try
            {
                serviceBus.Send(new[] {command, command2});
            }
            catch (AggregateException ex)
            {
                aggregateException = ex;
                Console.WriteLine(ex);
            }

            Assert.That(aggregateException != null, "No aggregate exception was thrown");
            Assert.That(aggregateException.InnerExceptions.Any(e => e.InnerException == originalException), "Aggregate exception did not contain original exception");

            A.CallTo(() => serviceEndpointClient1.Send(command)).MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => serviceEndpointClient2.Send(command)).MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => serviceEndpointClient3.Send(command)).MustHaveHappened(Repeated.Exactly.Once);
        }

        [Test]
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
            A.CallTo(() => serviceEndpoint2.Publish(ev)).Throws(originalException);

            IServiceBus serviceBus = new ServiceBus(new[] { serviceEndpointClient1, serviceEndpointClient2 }, new[] { serviceEndpoint1, serviceEndpoint2, serviceEndpoint3 });

            AggregateException aggregateException = null;
            try
            {
                serviceBus.Publish(ev);
            }
            catch (AggregateException ex)
            {
                aggregateException = ex;
                Console.WriteLine(ex);
            }

            Assert.That(aggregateException != null, "No aggregate exception was thrown");
            Assert.That(aggregateException.InnerExceptions.Any(e => e.InnerException == originalException), "Aggregate exception did not contain original exception");

            A.CallTo(() => serviceEndpoint1.Publish(ev)).MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => serviceEndpoint2.Publish(ev)).MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => serviceEndpoint3.Publish(ev)).MustHaveHappened(Repeated.Exactly.Once);
        }
        
        [Test]
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
            A.CallTo(() => serviceEndpoint2.Reply(request, response)).Throws(originalException);

            IServiceBus serviceBus = new ServiceBus(new[] { serviceEndpointClient1, serviceEndpointClient2 }, new[] { serviceEndpoint1, serviceEndpoint2, serviceEndpoint3 });

            AggregateException aggregateException = null;
            
            try
            {
                serviceBus.Reply(request, response);
            }
            catch (AggregateException ex)
            {
                aggregateException = ex;
                Console.WriteLine(ex);
            }

            Assert.That(aggregateException != null, "No aggregate exception was thrown");
            Assert.That(aggregateException.InnerExceptions.Any(e => e.InnerException == originalException), "Aggregate exception did not contain original exception");

            A.CallTo(() => serviceEndpoint1.Reply(request, response)).MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => serviceEndpoint2.Reply(request, response)).MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => serviceEndpoint3.Reply(request, response)).MustHaveHappened(Repeated.Exactly.Once);
        }

        [Test]
        public void TestEndpointWithErrorsDoesntEndSubscriptions()
        {
            TestServiceEndpoint erroringEndpoint = new TestServiceEndpoint(typeof(ITestServiceMessage1)) { ThrowException = true };
            TestServiceEndpoint serviceEndpoint = new TestServiceEndpoint(typeof(ITestServiceMessage2));

            IServiceBus serviceBus = ServiceBus.Configure()
                .WithEndpoint(erroringEndpoint as IServiceEndpointClient)
                .WithEndpoint(erroringEndpoint as IServiceEndpoint)
                .WithEndpoint(serviceEndpoint as IServiceEndpointClient)
                .WithEndpoint(serviceEndpoint as IServiceEndpoint)
                .Create();

            ConcurrentBag<IMessage> messages = new ConcurrentBag<IMessage>();
            ConcurrentBag<Exception> exceptions = new ConcurrentBag<Exception>();

            serviceBus.Events.Subscribe(messages.Add);
            serviceBus.Commands.Subscribe(messages.Add);
            serviceBus.Requests.Subscribe(messages.Add);
            serviceBus.Exceptions.Subscribe(exceptions.Add);

            // trigger exception
            serviceBus.Publish(new TestServiceMessage1());

            TestServiceMessage2 message1 = new TestServiceMessage2();
            serviceBus.Publish(message1);

            // trigger another exception
            serviceBus.Publish(new TestServiceMessage1());

            TestServiceMessage2 message2 = new TestServiceMessage2();
            serviceBus.Publish(message2);

            // wait some time until we think all messages have been sent and received
            Thread.Sleep(TimeSpan.FromSeconds(3));

            Assert.That(exceptions.Count(), Is.EqualTo(2));
            Assert.That(messages.Contains(message1), "message1 not received");
            Assert.That(messages.Contains(message2), "message2 not received");
        }
    }

    public interface ITestServiceMessage1 : IMessage {}
    public interface ITestServiceMessage2 : IMessage {}
    public class TestServiceMessage2 : ITestServiceMessage2, IEvent { }
    public class TestServiceMessage1 : ITestServiceMessage1, IEvent { }

    public class TestServiceEndpoint : IServiceEndpointClient, IServiceEndpoint
    {
        private readonly Subject<IMessage> _subject = new Subject<IMessage>();
        private readonly Type _serviceType;
        public bool ThrowException { get; set; }

        public TestServiceEndpoint(Type serviceType)
        {
            _serviceType = serviceType;
        }

        public bool CanHandle(IMessage message)
        {
            return _serviceType.IsInstanceOfType(message);
        }

        public void Send(ICommand command)
        {
            _subject.OnNext(command);
        }

        public IObservable<IResponse> GetResponses(IRequest request)
        {
            return Observable.Create<IResponse>(observer =>
                    _subject.OfType<IResponse>().Where(response => response.RequestId == request.RequestId && response.RequesterId == request.RequesterId).Select(ev =>
                    {
                        if (ThrowException)
                        {
                            throw new Exception();
                        }
                        return ev;
                    }).Subscribe(observer));
        }

        public IObservable<IEvent> Events
        {
            get
            {
                return Observable.Create<IEvent>(observer =>
                    _subject.OfType<IEvent>().Select(ev =>
                    {
                        if (ThrowException)
                        {
                            throw new Exception();
                        }
                        return ev;
                    }).Subscribe(observer));
            }
        }

        public IObservable<IRequest> Requests
        {
            get
            {
                return Observable.Create<IRequest>(observer =>
                    _subject.OfType<IRequest>().Select(request =>
                    {
                        if (ThrowException)
                        {
                            throw new Exception();
                        }
                        return request;
                    }).Subscribe(observer));
            }
        }

        public IObservable<ICommand> Commands
        {
            get
            {
                return Observable.Create<ICommand>(observer =>
                    _subject.OfType<ICommand>().Select(command =>
                    {
                        if (ThrowException)
                        {
                            throw new Exception();
                        }
                        return command;
                    }).Subscribe(observer));
            }
        }

        public void Publish(IEvent ev)
        {
            _subject.OnNext(ev);
        }

        public void Reply(IRequest request, IResponse response)
        {
            response.RequestId = request.RequestId;
            response.RequesterId = request.RequesterId;
            _subject.OnNext(response);
        }
    }
}