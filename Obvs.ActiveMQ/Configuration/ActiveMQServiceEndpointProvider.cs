using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reflection;
using Apache.NMS;
using Apache.NMS.ActiveMQ;
using Apache.NMS.ActiveMQ.Commands;
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
        private readonly string _selector;
        private readonly Func<IDictionary, bool> _propertyFilter;
        private readonly Func<TMessage, Dictionary<string, object>> _propertyProvider;
        private readonly Lazy<IConnection> _endpointConnection;
        private readonly Lazy<IConnection> _endpointClientConnection;
        private readonly bool _noLocal;

        public ActiveMQServiceEndpointProvider(string serviceName,
            string brokerUri,
            IMessageSerializer serializer,
            IMessageDeserializerFactory deserializerFactory,
            List<Tuple<Type, AcknowledgementMode>> queueTypes,
            Func<Assembly, bool> assemblyFilter = null,
            Func<Type, bool> typeFilter = null,
            Lazy<IConnection> sharedConnection = null,
            string selector = null,
            Func<IDictionary, bool> propertyFilter = null,
            Func<TMessage, Dictionary<string, object>> propertyProvider = null,
            string userName = null,
            string password = null,
            Action<ConnectionFactory> connectionFactoryConfiguration = null,
            bool noLocal = false)
            : base(serviceName)
        {
            _serializer = serializer;
            _deserializerFactory = deserializerFactory;
            _queueTypes = queueTypes;
            _assemblyFilter = assemblyFilter;
            _typeFilter = typeFilter;
            _selector = selector;
            _propertyFilter = propertyFilter;
            _propertyProvider = propertyProvider;
            _noLocal = noLocal;

            if (string.IsNullOrEmpty(brokerUri) && sharedConnection == null)
            {
                throw new InvalidOperationException(string.Format("For service endpoint '{0}', please specify a brokerUri to connect to. To do this you can use either ConnectToBroker() per endpoint, or WithActiveMQSharedConnectionScope() to share a connection across multiple endpoints.", serviceName));
            }

            if (sharedConnection == null)
            {
                ConnectionFactory endpointConnectionFactory = new ConnectionFactory(brokerUri, ConnectionClientId.CreateWithSuffix(string.Format("{0}.Endpoint", serviceName))) { CopyMessageOnSend = false };
                ConnectionFactory endpointClientConnectionFactory = new ConnectionFactory(brokerUri, ConnectionClientId.CreateWithSuffix(string.Format("{0}.EndpointClient", serviceName))) { CopyMessageOnSend = false };

                if (connectionFactoryConfiguration != null)
                {
                    connectionFactoryConfiguration(endpointConnectionFactory);
                    connectionFactoryConfiguration(endpointClientConnectionFactory);
                }

                _endpointConnection = endpointConnectionFactory.CreateLazyConnection(userName, password);
                _endpointClientConnection = endpointClientConnectionFactory.CreateLazyConnection(userName, password);
            }
            else
            {
                _endpointConnection = sharedConnection;
                _endpointClientConnection = sharedConnection;
            }
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
            List<Tuple<Type, AcknowledgementMode>> queueTypes;
            var acknowledgementMode = GetAcknowledgementMode<T>();

            if (TryGetMultipleQueueTypes<T>(out queueTypes))
            {
                var deserializers = _deserializerFactory.Create<T, TServiceMessage>(_assemblyFilter, _typeFilter).ToArray();
                var topicSources = new[]
                {
                    new MessageSource<T>(connection, deserializers, new ActiveMQTopic(destination),
                        acknowledgementMode, _selector, _propertyFilter, _noLocal)
                };
                var queueSources = queueTypes.Select(qt =>
                    new MessageSource<T>(connection,
                        deserializers,
                        new ActiveMQQueue(GetTypedQueueName(destination, qt.Item1)),
                        qt.Item2,
                        _selector,
                        _propertyFilter,
                        _noLocal));

                return new MergedMessageSource<T>(topicSources.Concat(queueSources));
            }

            return DestinationFactory.CreateSource<T, TServiceMessage>(connection, destination, GetDestinationType<T>(),
                _deserializerFactory, _propertyFilter, _assemblyFilter, _typeFilter, _selector,
                acknowledgementMode, _noLocal);
        }

        private IMessagePublisher<T> CreatePublisher<T>(Lazy<IConnection> connection, string destination) where T : class, TMessage
        {
            List<Tuple<Type, AcknowledgementMode>> queueTypes;

            if (TryGetMultipleQueueTypes<T>(out queueTypes))
            {
                var topicTypes = MessageTypes.Get<T, TServiceMessage>().Where(type => queueTypes.All(qt => qt.Item1 != type));
                var topicPublisher = new MessagePublisher<T>(connection, new ActiveMQTopic(destination), _serializer,
                    _propertyProvider, Scheduler.Default);
                var topicPublishers = topicTypes.Select(tt => new KeyValuePair<Type, IMessagePublisher<T>>(tt, topicPublisher));
                var queuePubishers = queueTypes.Select(qt =>
                    new KeyValuePair<Type, IMessagePublisher<T>>(qt.Item1,
                        new MessagePublisher<T>(
                            connection,
                            new ActiveMQQueue(GetTypedQueueName(destination, qt.Item1)),
                            _serializer,
                            _propertyProvider,
                            Scheduler.Default)));

                return new TypeRoutingMessagePublisher<T>(topicPublishers.Concat(queuePubishers));
            }

            return DestinationFactory.CreatePublisher<T>(connection,
                destination,
                GetDestinationType<T>(),
                _serializer,
                _propertyProvider);
        }

        private static string GetTypedQueueName(string destination, Type type)
        {
            return string.Format("{0}.{1}", destination, type.Name);
        }

        private bool TryGetMultipleQueueTypes<T>(out List<Tuple<Type, AcknowledgementMode>> queueTypes) where T : class, TMessage
        {
            queueTypes = _queueTypes.Where(qt => typeof(T).IsAssignableFrom(qt.Item1)).ToList();
            var moreThanOneQueueType = queueTypes.Count > 1;
            var containsSuperInterface = queueTypes.Any(qt => qt.Item1 == typeof(T));
            var multipleQueues = moreThanOneQueueType && !containsSuperInterface;
            if (!multipleQueues)
            {
                queueTypes = null;
            }
            return queueTypes != null;
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
            return _queueTypes.Any(q => q.Item1 == typeof(T)) ? DestinationType.Queue : DestinationType.Topic;
        }

        private AcknowledgementMode GetAcknowledgementMode<T>() where T : TMessage
        {
            return _queueTypes.Where(q => q.Item1 == typeof(T)).Select(q => q.Item2).FirstOrDefault();
        }

        private static IDisposable GetConnectionDisposable(Lazy<IConnection> lazyConnection)
        {
            return Disposable.Create(() =>
            {
                if (lazyConnection.IsValueCreated &&
                    lazyConnection.Value.IsStarted)
                {
                    lazyConnection.Value.Stop();
                    lazyConnection.Value.Close();
                    lazyConnection.Value.Dispose();
                }
            });
        }
    }
}