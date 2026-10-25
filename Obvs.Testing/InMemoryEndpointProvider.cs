using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Obvs.Configuration;
using Obvs.Types;

namespace Obvs.Testing
{
    // One destination per service and kind of message, as the broker-based transports name them
    internal sealed class InMemoryEndpointProvider : IServiceEndpointProvider
    {
        private readonly string _serviceName;
        private readonly Type _serviceMessageType;
        private readonly InMemoryBroker _broker;

        public InMemoryEndpointProvider(string serviceName, Type serviceMessageType, InMemoryBroker broker)
        {
            _serviceName = serviceName;
            _serviceMessageType = serviceMessageType;
            _broker = broker;
        }

        public IServiceEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse> CreateEndpoint()
        {
            return new ServiceEndpoint(
                Source<IRequest>("Requests"),
                Source<ICommand>("Commands"),
                Publisher<IEvent>("Events"),
                Publisher<IResponse>("Responses"),
                _serviceMessageType);
        }

        public IServiceEndpointClient<IMessage, ICommand, IEvent, IRequest, IResponse> CreateEndpointClient()
        {
            return new ServiceEndpointClient(
                Source<IEvent>("Events"),
                Source<IResponse>("Responses"),
                Publisher<IRequest>("Requests"),
                Publisher<ICommand>("Commands"),
                _serviceMessageType);
        }

        private IMessageSource<TMessage> Source<TMessage>(string kind) where TMessage : class, IMessage
        {
            return new InMemoryMessageSource<TMessage>(_broker, Destination(kind));
        }

        private IMessagePublisher<TMessage> Publisher<TMessage>(string kind) where TMessage : class, IMessage
        {
            return new InMemoryMessagePublisher<TMessage>(_broker, _serviceName, Destination(kind));
        }

        private string Destination(string kind)
        {
            return $"{_serviceName}.{kind}";
        }

        private sealed class InMemoryMessageSource<TMessage> : IMessageSource<TMessage> where TMessage : class, IMessage
        {
            private readonly InMemoryBroker _broker;
            private readonly string _destination;

            public InMemoryMessageSource(InMemoryBroker broker, string destination)
            {
                _broker = broker;
                _destination = destination;
            }

            public IObservable<TMessage> Messages => _broker.Receive(_destination).OfType<TMessage>();

            public void Dispose()
            {
            }
        }

        private sealed class InMemoryMessagePublisher<TMessage> : IMessagePublisher<TMessage> where TMessage : class, IMessage
        {
            private readonly InMemoryBroker _broker;
            private readonly string _serviceName;
            private readonly string _destination;

            public InMemoryMessagePublisher(InMemoryBroker broker, string serviceName, string destination)
            {
                _broker = broker;
                _serviceName = serviceName;
                _destination = destination;
            }

            public Task PublishAsync(TMessage message)
            {
                _broker.Send(_serviceName, _destination, message);
                return Task.CompletedTask;
            }

            public void Dispose()
            {
            }
        }
    }
}
