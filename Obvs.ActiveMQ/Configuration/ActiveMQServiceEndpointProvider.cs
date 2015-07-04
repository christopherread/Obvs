using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reflection;
using System.Threading;
using Apache.NMS;
using Apache.NMS.ActiveMQ;
using Apache.NMS.ActiveMQ.Commands;
using Obvs.Configuration;
using Obvs.MessageProperties;
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
                    CreateSource<TRequest>(_endpointConnection, RequestsDestination),
                    CreateSource<TCommand>(_endpointConnection, CommandsDestination),
                    CreatePublisher<TEvent>(_endpointConnection, EventsDestination),
                    CreatePublisher<TResponse>(_endpointConnection, ResponsesDestination),
                    typeof(TServiceMessage)), 
                GetConnectionDisposable(_endpointConnection));
        }

        private IMessageSource<T> CreateSource<T>(Lazy<IConnection> connection, string destination) where T : class, TMessage
        {
            var queueTypes = _queueTypes.Where(t => typeof(T).IsAssignableFrom(t.Item1)).ToArray();
            var moreThanOneQueueType = queueTypes.Length > 1;
            var containsSuperInterface = queueTypes.Any(qt => qt.Item1 == typeof(T));

            if (moreThanOneQueueType && !containsSuperInterface)
            {
                var deserializers = _deserializerFactory.Create<T, TServiceMessage>(_assemblyFilter, _typeFilter);

                return new MergedMessageSource<T>(queueTypes.Select(qt =>
                    new MessageSource<T>(
                        connection,
                        deserializers,
                        new ActiveMQQueue(string.Format("{0}.{1}", destination, typeof (T).Name)),
                        qt.Item2)));
            }

            return DestinationFactory.CreateSource<T, TServiceMessage>(connection, destination, GetDestinationType<T>(), _deserializerFactory, _assemblyFilter, _typeFilter, null, GetAcknowledgementMode<T>());
        }

        private IMessagePublisher<T> CreatePublisher<T>(Lazy<IConnection> connection, string destination) where T : class, TMessage
        {
            var queueTypes = _queueTypes.Where(t => typeof(T).IsAssignableFrom(t.Item1)).ToArray();
            var moreThanOneQueueType = queueTypes.Length > 1;
            var containsSuperInterface = queueTypes.Any(qt => qt.Item1 == typeof (T));

            if (moreThanOneQueueType && !containsSuperInterface)
            {
                return new TypeRoutingMessagePublisher<T>(queueTypes.Select(qt =>
                    new KeyValuePair<Type, IMessagePublisher<T>>(qt.Item1,
                        new MessagePublisher<T>(
                            connection,
                            new ActiveMQQueue(string.Format("{0}.{1}", destination, typeof (T).Name)),
                            _serializer,
                            new DefaultPropertyProvider<T>(),
                            _scheduler))));
            }

            return DestinationFactory.CreatePublisher<T>(connection, destination, GetDestinationType<T>(), _serializer, _scheduler);
        }

        public override IServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse> CreateEndpointClient()
        {
            return new DisposingServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse>(
                new ServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse>(
                    CreateSource<TEvent>(_endpointClientConnection, EventsDestination),
                    CreateSource<TResponse>(_endpointClientConnection, ResponsesDestination),
                    CreatePublisher<TRequest>(_endpointClientConnection, RequestsDestination),
                    CreatePublisher<TCommand>(_endpointClientConnection, CommandsDestination),
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