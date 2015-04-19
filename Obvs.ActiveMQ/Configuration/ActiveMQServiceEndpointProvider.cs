using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Threading;
using Apache.NMS;
using Obvs.Configuration;
using Obvs.Serialization;
using Obvs.Types;
using IMessage = Obvs.Types.IMessage;

namespace Obvs.ActiveMQ.Configuration
{
    public class ActiveMQServiceEndpointProvider<TServiceMessage> : ServiceEndpointProviderBase where TServiceMessage : IMessage
    {
        private readonly string _brokerUri;
        private readonly IMessageSerializer _serializer;
        private readonly IMessageDeserializerFactory _deserializerFactory;
        private readonly List<Type> _queueTypes;
        private readonly string _assemblyNameContains;
        private readonly IScheduler _scheduler;

        public ActiveMQServiceEndpointProvider(string serviceName, string brokerUri, 
                                               IMessageSerializer serializer, IMessageDeserializerFactory deserializerFactory, 
                                               List<Type> queueTypes, string assemblyNameContains)
            : base(serviceName)
        {
            _brokerUri = brokerUri;
            _serializer = serializer;
            _deserializerFactory = deserializerFactory;
            _queueTypes = queueTypes;
            _assemblyNameContains = assemblyNameContains;
            _scheduler = new EventLoopScheduler(start => new Thread(start){Name = string.Format("{0}.Publisher", serviceName), IsBackground = true});
        }

        public override IServiceEndpoint CreateEndpoint()
        {
            return new ServiceEndpoint(
                DestinationFactory.CreateSource<IRequest, TServiceMessage>(_brokerUri, RequestsDestination, ServiceName, GetDestinationType<IRequest>(), _deserializerFactory, _assemblyNameContains),
                DestinationFactory.CreateSource<ICommand, TServiceMessage>(_brokerUri, CommandsDestination, ServiceName, GetDestinationType<ICommand>(), _deserializerFactory, _assemblyNameContains),
                DestinationFactory.CreatePublisher<IEvent>(_brokerUri, EventsDestination, ServiceName, GetDestinationType<IEvent>(), _serializer, _scheduler),
                DestinationFactory.CreatePublisher<IResponse>(_brokerUri, ResponsesDestination, ServiceName, GetDestinationType<IResponse>(), _serializer, _scheduler),
                typeof(TServiceMessage));
        }

        public override IServiceEndpointClient CreateEndpointClient()
        {
            return new ServiceEndpointClient(
                DestinationFactory.CreateSource<IEvent, TServiceMessage>(_brokerUri, EventsDestination, ServiceName, GetDestinationType<IEvent>(), _deserializerFactory, _assemblyNameContains),
                DestinationFactory.CreateSource<IResponse, TServiceMessage>(_brokerUri, ResponsesDestination, ServiceName, GetDestinationType<IResponse>(), _deserializerFactory, _assemblyNameContains),
                DestinationFactory.CreatePublisher<IRequest>(_brokerUri, RequestsDestination, ServiceName, GetDestinationType<IRequest>(), _serializer, _scheduler),
                DestinationFactory.CreatePublisher<ICommand>(_brokerUri, CommandsDestination, ServiceName, GetDestinationType<ICommand>(), _serializer, _scheduler),
                typeof(TServiceMessage));
        }

        private DestinationType GetDestinationType<TMessage>() where TMessage : IMessage
        {
            return _queueTypes.Contains(typeof (TMessage)) ? DestinationType.Queue : DestinationType.Topic;
        }
    }
}