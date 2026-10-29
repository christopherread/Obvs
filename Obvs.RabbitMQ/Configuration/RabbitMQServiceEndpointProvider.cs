using System;
using System.Reactive.Disposables;
using System.Reflection;
using System.Threading;
using Obvs.Configuration;
using Obvs.Serialization;
using RabbitMQ.Client;

// v6's RabbitMQ transport building blocks, which UseRabbitMq(...) is built on: its public members are exempt from the rule that every one is documented
#pragma warning disable CS1570, CS1572, CS1591

namespace Obvs.RabbitMQ.Configuration
{
    public class RabbitMQServiceEndpointProvider<TServiceMessage, TMessage, TCommand, TEvent, TRequest, TResponse> : ServiceEndpointProviderBase<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TServiceMessage : class
        where TMessage : class
        where TCommand : class, TMessage
        where TEvent : class, TMessage
        where TRequest : class, TMessage
        where TResponse : class, TMessage
    {
        private readonly string _brokerUri;
        private readonly IMessageSerializer _serializer;
        private readonly IMessageDeserializerFactory _deserializerFactory;
        private readonly Func<Assembly, bool> _assemblyFilter;
        private readonly Func<Type, bool> _typeFilter;
        private readonly Lazy<IConnection> _connection;
        private readonly string _uniqueQueueSuffix;

        /// <param name="uniqueQueueSuffix">Appended to queue names, so several buses in one process don't share them</param>
        public RabbitMQServiceEndpointProvider(string serviceName, string brokerUri, IMessageSerializer serializer, IMessageDeserializerFactory deserializerFactory, Func<Assembly, bool> assemblyFilter = null, Func<Type, bool> typeFilter = null,
            bool automaticRecoveryEnabled = true, string uniqueQueueSuffix = null)
            : base(serviceName)
        {
            _uniqueQueueSuffix = uniqueQueueSuffix;
            _brokerUri = brokerUri;
            _serializer = serializer;
            _deserializerFactory = deserializerFactory;
            _typeFilter = typeFilter ?? (type => true);
            _assemblyFilter = assemblyFilter ?? (assembly => true);

            _connection = new Lazy<IConnection>(() =>
            {
                var connectionFactory = new ConnectionFactory
                {
                    Uri =  new Uri(brokerUri),
                    AutomaticRecoveryEnabled = automaticRecoveryEnabled,
                };
                var conn = connectionFactory.CreateConnectionAsync().GetAwaiter().GetResult();
                return conn;
            }, LazyThreadSafetyMode.ExecutionAndPublication);
        }

        public override IServiceEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse> CreateEndpoint()
        {
            return new ServiceEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse>(
                SourcePublisherFactory.CreateSource<TRequest, TServiceMessage>(_brokerUri, RequestsDestination, ServiceName, _deserializerFactory, _connection, _assemblyFilter, _typeFilter, _uniqueQueueSuffix),
                SourcePublisherFactory.CreateSource<TCommand, TServiceMessage>(_brokerUri, CommandsDestination, ServiceName, _deserializerFactory, _connection, _assemblyFilter, _typeFilter, _uniqueQueueSuffix),
                SourcePublisherFactory.CreatePublisher<TEvent>(_brokerUri, EventsDestination, ServiceName, _serializer, _connection),
                SourcePublisherFactory.CreatePublisher<TResponse>(_brokerUri, ResponsesDestination, ServiceName, _serializer, _connection),
                typeof(TServiceMessage));
        }

        public override IServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse> CreateEndpointClient()
        {

            return new DisposingServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse>(
                new ServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse>(
                SourcePublisherFactory.CreateSource<TEvent, TServiceMessage>(_brokerUri, EventsDestination, ServiceName, _deserializerFactory, _connection, _assemblyFilter, _typeFilter, _uniqueQueueSuffix),
                SourcePublisherFactory.CreateSource<TResponse, TServiceMessage>(_brokerUri, ResponsesDestination, ServiceName, _deserializerFactory, _connection, _assemblyFilter, _typeFilter, _uniqueQueueSuffix),
                SourcePublisherFactory.CreatePublisher<TRequest>(_brokerUri, RequestsDestination, ServiceName, _serializer, _connection),
                SourcePublisherFactory.CreatePublisher<TCommand>(_brokerUri, CommandsDestination, ServiceName, _serializer, _connection),
                typeof(TServiceMessage)),
                GetConnectionDisposable(_connection));
        }

        private static IDisposable GetConnectionDisposable(Lazy<IConnection> connection)
        {
            return Disposable.Create(() =>
            {
                if (connection.IsValueCreated)
                {
                    connection.Value.CloseAsync().GetAwaiter().GetResult();
                    connection.Value.Dispose();
                }
            });
        }
    }
}