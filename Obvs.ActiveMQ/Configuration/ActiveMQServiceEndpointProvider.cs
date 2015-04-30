using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Threading;
using Apache.NMS;
using Apache.NMS.ActiveMQ;
using Obvs.Configuration;
using Obvs.Serialization;
using Obvs.Types;
using IMessage = Obvs.Types.IMessage;

namespace Obvs.ActiveMQ.Configuration
{
    public class ActiveMQServiceEndpointProvider<TServiceMessage> : ServiceEndpointProviderBase where TServiceMessage : IMessage
    {
        private readonly IMessageSerializer _serializer;
        private readonly IMessageDeserializerFactory _deserializerFactory;
        private readonly List<Type> _queueTypes;
        private readonly string _assemblyNameContains;
        private readonly IScheduler _scheduler;
        private readonly Lazy<IConnection> _endpointConnection;
        private readonly Lazy<IConnection> _endpointClientConnection;

        public ActiveMQServiceEndpointProvider(string serviceName, string brokerUri, 
                                               IMessageSerializer serializer, IMessageDeserializerFactory deserializerFactory, 
                                               List<Type> queueTypes, string assemblyNameContains)
            : base(serviceName)
        {
            _serializer = serializer;
            _deserializerFactory = deserializerFactory;
            _queueTypes = queueTypes;
            _assemblyNameContains = assemblyNameContains;
            _scheduler = new EventLoopScheduler(start => new Thread(start){Name = string.Format("{0}.Publisher", serviceName), IsBackground = true});

            IConnectionFactory endpointConnectionFactory = new ConnectionFactory(brokerUri, ConnectionClientId.CreateWithSuffix(string.Format("{0}.Endpoint", serviceName)));
            IConnectionFactory endpointClientConnectionFactory = new ConnectionFactory(brokerUri, ConnectionClientId.CreateWithSuffix(string.Format("{0}.EndpointClient", serviceName)));
            _endpointConnection = endpointConnectionFactory.GetLazyConnection();           
            _endpointClientConnection = endpointClientConnectionFactory.GetLazyConnection();
        }

        public override IServiceEndpoint CreateEndpoint()
        {
            return new DisposingServiceEndpoint(
                new ServiceEndpoint(
                    DestinationFactory.CreateSource<IRequest, TServiceMessage>(_endpointConnection, RequestsDestination, GetDestinationType<IRequest>(), _deserializerFactory, _assemblyNameContains),
                    DestinationFactory.CreateSource<ICommand, TServiceMessage>(_endpointConnection, CommandsDestination, GetDestinationType<ICommand>(), _deserializerFactory, _assemblyNameContains),
                    DestinationFactory.CreatePublisher<IEvent>(_endpointConnection, EventsDestination, GetDestinationType<IEvent>(), _serializer, _scheduler),
                    DestinationFactory.CreatePublisher<IResponse>(_endpointConnection, ResponsesDestination, GetDestinationType<IResponse>(), _serializer, _scheduler),
                    typeof(TServiceMessage)), 
                GetConnectionDisposable(_endpointConnection));
        }

        public override IServiceEndpointClient CreateEndpointClient()
        {
            return new DisposingServiceEndpointClient(
                new ServiceEndpointClient(
                    DestinationFactory.CreateSource<IEvent, TServiceMessage>(_endpointClientConnection, EventsDestination, GetDestinationType<IEvent>(), _deserializerFactory, _assemblyNameContains),
                    DestinationFactory.CreateSource<IResponse, TServiceMessage>(_endpointClientConnection, ResponsesDestination, GetDestinationType<IResponse>(), _deserializerFactory, _assemblyNameContains),
                    DestinationFactory.CreatePublisher<IRequest>(_endpointClientConnection, RequestsDestination, GetDestinationType<IRequest>(), _serializer, _scheduler),
                    DestinationFactory.CreatePublisher<ICommand>(_endpointClientConnection, CommandsDestination, GetDestinationType<ICommand>(), _serializer, _scheduler),
                    typeof(TServiceMessage)),
                GetConnectionDisposable(_endpointClientConnection));
        }

        private DestinationType GetDestinationType<TMessage>() where TMessage : IMessage
        {
            return _queueTypes.Contains(typeof (TMessage)) ? DestinationType.Queue : DestinationType.Topic;
        }

        private static IDisposable GetConnectionDisposable(Lazy<IConnection> lazyConnection)
        {
            return Disposable.Create(() =>
            {
                if (lazyConnection.IsValueCreated)
                {
                    lazyConnection.Value.Stop();
                    lazyConnection.Value.Close();
                    lazyConnection.Value.Dispose();
                }
            });
        }
    }
}