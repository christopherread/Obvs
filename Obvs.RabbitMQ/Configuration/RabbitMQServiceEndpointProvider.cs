using System;
using System.Reflection;
using Obvs.Configuration;
using Obvs.Serialization;

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

        public RabbitMQServiceEndpointProvider(string serviceName, string brokerUri, IMessageSerializer serializer, IMessageDeserializerFactory deserializerFactory, Func<Assembly, bool> assemblyFilter = null, Func<Type, bool> typeFilter = null)
            : base(serviceName)
        {
            _brokerUri = brokerUri;
            _serializer = serializer;
            _deserializerFactory = deserializerFactory;
            _typeFilter = typeFilter ?? (type => true);
            _assemblyFilter = assemblyFilter ?? (assembly => true);
        }

        public override IServiceEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse> CreateEndpoint()
        {
            return new ServiceEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse>(
                SourcePublisherFactory.CreateSource<TRequest, TServiceMessage>(_brokerUri, RequestsDestination, ServiceName, _deserializerFactory, _assemblyFilter, _typeFilter),
                SourcePublisherFactory.CreateSource<TCommand, TServiceMessage>(_brokerUri, CommandsDestination, ServiceName, _deserializerFactory, _assemblyFilter, _typeFilter),
                SourcePublisherFactory.CreatePublisher<TEvent>(_brokerUri, EventsDestination, ServiceName, _serializer),
                SourcePublisherFactory.CreatePublisher<TResponse>(_brokerUri, ResponsesDestination, ServiceName, _serializer),
                typeof(TServiceMessage));
        }

        public override IServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse> CreateEndpointClient()
        {
            return new ServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse>(
                SourcePublisherFactory.CreateSource<TEvent, TServiceMessage>(_brokerUri, EventsDestination, ServiceName, _deserializerFactory, _assemblyFilter, _typeFilter),
                SourcePublisherFactory.CreateSource<TResponse, TServiceMessage>(_brokerUri, ResponsesDestination, ServiceName, _deserializerFactory, _assemblyFilter, _typeFilter),
                SourcePublisherFactory.CreatePublisher<TRequest>(_brokerUri, RequestsDestination, ServiceName, _serializer),
                SourcePublisherFactory.CreatePublisher<TCommand>(_brokerUri, CommandsDestination, ServiceName, _serializer),
                typeof(TServiceMessage));
        }
    }
}