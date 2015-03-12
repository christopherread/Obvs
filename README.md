
# Obvs: convention based observable µService bus
## observable services, *obvs*

Features:

* Simple RX based interfaces for doing pub/sub
* Convention based messaging over topics per service
* Multiplexing of multiple message types over single topics/queues
* Dynamic creation of deserializers per type
* Asynchronous error handling on a separate channel
* Easy to extend and customise, allowing integration with external systems
* Fluent code based configuration
* Supports ActiveMQ and NetMQ transports
* Provides serialization for XML, JSON, and ProtoBuf

## Example

Define a root message type to identify messages as belonging to your service:

	public interface ITestServiceMessage : IMessage { }

Create command/event/request/response message types:

	public class TestCommand : ITestServiceMessage, ICommand { }

	public class TestEvent : ITestServiceMessage, IEvent { }

	public class TestRequest: ITestServiceMessage, IRequest { }
	
	public class TestResponse : ITestServiceMessage, IResponse { }

Create your service bus:

	IServiceBus serviceBus = ServiceBus.Configure()
        .WithActiveMqEndpoints<ITestServiceMessage>()
            .Named("Obvs.TestService")
            .UsingBroker("tcp://localhost:61616")
            .AsClientAndServer()
        .Create();

Send commands:

	serviceBus.Commands.Subscribe(e => Console.WriteLine("Received a command!"));
	serviceBus.Send(new TestCommand())

Publish events:

	serviceBus.Events.Subscribe(e => Console.WriteLine("Received an event!"));
	serviceBus.Publish(new TestEvent())
	
Request/response:
	
	serviceBus
		.GetResponses(new TestRequest())
		.OfType<TestResponse>()
		.Take(1)
		.Timeout(TimeSpan.FromSeconds(1))
		.Subscribe(r => Console.WriteLine("Received a response!"), exception => Console.WriteLine("Oh no!"));

	serviceBus.Requests.OfType<TestRequest>.Subscribe(request => serviceBus.Reply(request, new TestResponse()));

Define custom endpoints that can wrap API calls or integrations with other systems:
	
	public class MyCustomEndpoint : IServiceEndpointClient
    	{
        	Type _serviceType = typeof(IMyCustomServiceMessage);

        	public IObservable<IEvent> Events
        	{
            		get
            		{
                		// subscribe to external MQ broker
            		}
        	}

        	public void Send(ICommand command)
        	{
            		// call external API
        	}

        	public IObservable<IResponse> GetResponses(IRequest request)
        	{
            		// call external API and wrap response in observable
        	}

        	public bool CanHandle(IMessage message)
        	{
            		return _serviceType.IsInstanceOfType(message);
        	}
    	}
		
	...

	IServiceBus serviceBus = ServiceBus.Configure()
		.WithBroker("tcp://localhost:61616")
		.WithActiveMqEndpoints<ITestServiceMessage>("Obvs.TestService")
		.WithEndpoints(new MyCustomEndpoint())
		.Create();