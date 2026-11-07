using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Threading;
using Microsoft.Extensions.Logging;
using Obvs.Configuration;
using Obvs.RabbitMQ.Configuration;
using Obvs.Serialization;
using Obvs.Transports;
using Obvs.Types;
using RabbitMQ.Client;

namespace Obvs.RabbitMQ.SharedQueues
{
    // What UseRabbitMq builds a service's endpoints with. Commands and requests go to quorum queues the service's servers
    // share; events and responses to a queue of each bus's own, as in v6. Generic in the service's message type, which picks
    // the deserializers for the messages the service owns.
    internal sealed class RabbitMqEndpointProvider<TServiceMessage> : ServiceEndpointProviderBase<IMessage, ICommand, IEvent, IRequest, IResponse>
        where TServiceMessage : class
    {
        private readonly string _instanceId;
        private readonly RabbitMqOptions _options;
        private readonly Func<Type, int> _concurrency;
        private readonly IMessageSerializer _serializer;
        private readonly IMessageDeserializerFactory _deserializerFactory;
        private readonly SharedQueueTopology _topology;

        /// <param name="concurrency">How many messages of each kind to take at once</param>
        public RabbitMqEndpointProvider(string serviceName, string instanceId, RabbitMqOptions options, Func<Type, int> concurrency,
            IMessageSerializer serializer, IMessageDeserializerFactory deserializerFactory, bool isServer)
            : base(serviceName)
        {
            _instanceId = instanceId;
            _options = options;
            _concurrency = concurrency;
            _serializer = serializer;
            _deserializerFactory = deserializerFactory;
            _topology = new SharedQueueTopology(serviceName, options.MaxDeliveryAttempts);
            if (isServer)
            {
                DeclareQueues();
            }
        }

        public override IServiceEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse> CreateEndpoint()
        {
            var connection = Connect();
            return new DisposingServiceEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse>(
                new ServiceEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse>(
                    new SharedQueueSource<IRequest>(connection, _topology, RequestsDestination, Deserializers<IRequest>(), _concurrency(typeof(IRequest)), _options.Logger),
                    new SharedQueueSource<ICommand>(connection, _topology, CommandsDestination, Deserializers<ICommand>(), _concurrency(typeof(ICommand)), _options.Logger),
                    new MessagePublisher<IEvent>(connection, _serializer, ServiceName, EventsDestination),
                    new MessagePublisher<IResponse>(connection, _serializer, ServiceName, ResponsesDestination),
                    typeof(TServiceMessage)),
                Closing(connection));
        }

        public override IServiceEndpointClient<IMessage, ICommand, IEvent, IRequest, IResponse> CreateEndpointClient()
        {
            var connection = Connect();
            return new DisposingServiceEndpointClient<IMessage, ICommand, IEvent, IRequest, IResponse>(
                new ServiceEndpointClient<IMessage, ICommand, IEvent, IRequest, IResponse>(
                    new MessageSource<IEvent>(connection, Deserializers<IEvent>(), ServiceName, EventsDestination, _instanceId),
                    new MessageSource<IResponse>(connection, Deserializers<IResponse>(), ServiceName, ResponsesDestination, _instanceId),
                    new MessagePublisher<IRequest>(connection, _serializer, ServiceName, RequestsDestination),
                    new MessagePublisher<ICommand>(connection, _serializer, ServiceName, CommandsDestination),
                    typeof(TServiceMessage)),
                Closing(connection));
        }

        // As the server starts, so commands sent while none of the service's servers receives wait on its queue. Only servers
        // declare them: a client of a v6 server would otherwise fill a queue nothing reads, which a v7 server would then
        // handle again. A failure is logged, not thrown: the sources declare the queues again before they receive, and
        // report it then.
        private void DeclareQueues()
        {
            try
            {
                var connection = CreateConnection();
                try
                {
                    foreach (var queue in new[] { CommandsDestination, RequestsDestination })
                    {
                        // A declaration that fails closes its channel
                        var channel = connection.CreateChannelAsync().GetAwaiter().GetResult();
                        try
                        {
                            _topology.DeclareAsync(channel, queue).GetAwaiter().GetResult();
                        }
                        finally
                        {
                            channel.Dispose();
                        }
                    }
                }
                finally
                {
                    connection.CloseAsync().GetAwaiter().GetResult();
                    connection.Dispose();
                }
            }
            catch (Exception exception)
            {
                _options.Logger.LogWarning(exception, "RabbitMQ couldn't declare service {ServiceName}'s queues as the bus started, " +
                                                      "and tries again before it receives: {Message}", ServiceName, exception.Message);
            }
        }

        // Each endpoint has its own connection, so one host stopping can't close another's, and the broker gives back what
        // a stopped endpoint hadn't settled
        private Lazy<IConnection> Connect()
        {
            return new Lazy<IConnection>(CreateConnection, LazyThreadSafetyMode.ExecutionAndPublication);
        }

        private IConnection CreateConnection()
        {
            return new ConnectionFactory
            {
                Uri = _options.Uri,
                AutomaticRecoveryEnabled = _options.AutomaticRecoveryEnabled,
            }.CreateConnectionAsync().GetAwaiter().GetResult();
        }

        private static IDisposable Closing(Lazy<IConnection> connection)
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

        private IMessageDeserializer<T>[] Deserializers<T>() where T : class
        {
            return _deserializerFactory.Create<T, TServiceMessage>().ToArray();
        }
    }
}
