using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reflection;
using System.Threading;
using Apache.NMS;
using Apache.NMS.ActiveMQ;
using Obvs.Configuration;
using Obvs.Serialization;

namespace Obvs.ActiveMQ.Configuration
{
    public class ActiveMQServiceEndpointProvider<TServiceMessage, TMessage, TCommand, TEvent, TRequest, TResponse> : ServiceEndpointProviderBase<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TMessage : class
        where TServiceMessage : class  
        where TCommand : class, TMessage 
        where TEvent : class, TMessage 
        where TRequest : class, TMessage 
        where TResponse : class, TMessage
    {
        private readonly IMessageSerializer _serializer;
        private readonly IMessageDeserializerFactory _deserializerFactory;
        private readonly List<Tuple<Type, AcknowledgementMode>> _queueTypes;
        private readonly Func<Assembly, bool> _assemblyFilter;
        private readonly Func<Type, bool> _typeFilter;
        private readonly IScheduler _scheduler;
        private readonly Lazy<IConnection> _endpointConnection;
        private readonly Lazy<IConnection> _endpointClientConnection;

        public ActiveMQServiceEndpointProvider(string serviceName, string brokerUri, 
                                               IMessageSerializer serializer, IMessageDeserializerFactory deserializerFactory,
                                               List<Tuple<Type, AcknowledgementMode>> queueTypes, Func<Assembly, bool> assemblyFilter = null, Func<Type, bool> typeFilter = null)
            : base(serviceName)
        {
            _serializer = serializer;
            _deserializerFactory = deserializerFactory;
            _queueTypes = queueTypes;
            _assemblyFilter = assemblyFilter;
            _typeFilter = typeFilter;
            
            _scheduler = new EventLoopScheduler(start => new Thread(start){Name = string.Format("{0}.Publisher", serviceName), IsBackground = true});

            IConnectionFactory endpointConnectionFactory = new ConnectionFactory(brokerUri, ConnectionClientId.CreateWithSuffix(string.Format("{0}.Endpoint", serviceName)));
            IConnectionFactory endpointClientConnectionFactory = new ConnectionFactory(brokerUri, ConnectionClientId.CreateWithSuffix(string.Format("{0}.EndpointClient", serviceName)));
            _endpointConnection = endpointConnectionFactory.GetLazyConnection();           
            _endpointClientConnection = endpointClientConnectionFactory.GetLazyConnection();
        }

        public override IServiceEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse> CreateEndpoint()
        {
            return new DisposingServiceEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse>(
                new ServiceEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse>(
                    DestinationFactory.CreateSource<TRequest, TServiceMessage>(_endpointConnection, RequestsDestination, GetDestinationType<TRequest>(), _deserializerFactory, _assemblyFilter, _typeFilter, null, GetAcknowledgementMode<TRequest>()),
                    DestinationFactory.CreateSource<TCommand, TServiceMessage>(_endpointConnection, CommandsDestination, GetDestinationType<TCommand>(), _deserializerFactory, _assemblyFilter, _typeFilter, null, GetAcknowledgementMode<TCommand>()),
                    DestinationFactory.CreatePublisher<TEvent>(_endpointConnection, EventsDestination, GetDestinationType<TEvent>(), _serializer, _scheduler),
                    DestinationFactory.CreatePublisher<TResponse>(_endpointConnection, ResponsesDestination, GetDestinationType<TResponse>(), _serializer, _scheduler),
                    typeof(TServiceMessage)), 
                GetConnectionDisposable(_endpointConnection));
        }

        public override IServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse> CreateEndpointClient()
        {
            return new DisposingServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse>(
                new ServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse>(
                    DestinationFactory.CreateSource<TEvent, TServiceMessage>(_endpointClientConnection, EventsDestination, GetDestinationType<TEvent>(), _deserializerFactory, _assemblyFilter, _typeFilter, null, GetAcknowledgementMode<TEvent>()),
                    DestinationFactory.CreateSource<TResponse, TServiceMessage>(_endpointClientConnection, ResponsesDestination, GetDestinationType<TResponse>(), _deserializerFactory, _assemblyFilter, _typeFilter, null, GetAcknowledgementMode<TResponse>()),
                    DestinationFactory.CreatePublisher<TRequest>(_endpointClientConnection, RequestsDestination, GetDestinationType<TRequest>(), _serializer, _scheduler),
                    DestinationFactory.CreatePublisher<TCommand>(_endpointClientConnection, CommandsDestination, GetDestinationType<TCommand>(), _serializer, _scheduler),
                    typeof(TServiceMessage)),
                GetConnectionDisposable(_endpointClientConnection));
        }

        private DestinationType GetDestinationType<T>() where T : TMessage
        {
            return _queueTypes.Any(q => q.Item1 == typeof (T)) ? DestinationType.Queue : DestinationType.Topic;
        }

        private AcknowledgementMode GetAcknowledgementMode<T>() where T : TMessage
        {
            return _queueTypes.Where(q => q.Item1 == typeof (T)).Select(q => q.Item2).FirstOrDefault();
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