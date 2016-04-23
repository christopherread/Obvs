using System;
using System.Reflection;
using Obvs.Configuration;
using Obvs.Serialization;

namespace Obvs.NetMQ.Configuration
{
    public class NetMqServiceEndpointProvider<TServiceMessage, TMessage, TCommand, TEvent, TRequest, TResponse> : 
        ServiceEndpointProviderBase<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TServiceMessage : class
        where TMessage : class
        where TCommand : class, TMessage
        where TEvent : class, TMessage
        where TRequest : class, TMessage
        where TResponse : class, TMessage
    {
        private readonly IMessageSerializer _serializer;
        private readonly IMessageDeserializerFactory _deserializerFactory;
        private readonly Func<Assembly, bool> _assemblyFilter;
        private readonly Func<Type, bool> _typeFilter;
        private readonly string _requestAddress;
        private readonly string _responseAddress;
        private readonly string _commandAddress;
        private readonly string _eventAddress;
        
        public NetMqServiceEndpointProvider(string serviceName, string address, int port, 
            IMessageSerializer serializer, IMessageDeserializerFactory deserializerFactory, 
            Func<Assembly, bool> assemblyFilter, Func<Type, bool> typeFilter)
            : base(serviceName)
        {
            _serializer = serializer;
            _deserializerFactory = deserializerFactory;
            _assemblyFilter = assemblyFilter;
            _typeFilter = typeFilter;
            _requestAddress = string.Format("{0}:{1}", address, port + 0);
            _responseAddress = string.Format("{0}:{1}", address, port + 1);
            _commandAddress = string.Format("{0}:{1}", address, port + 2);
            _eventAddress = string.Format("{0}:{1}", address, port + 3);
        }

        public override IServiceEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse> CreateEndpoint()
        {
            const SocketType socketType = SocketType.Server;
            return new ServiceEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse>(
               new MessageSource<TRequest>(_requestAddress, _deserializerFactory.Create<TRequest, TServiceMessage>(_assemblyFilter, _typeFilter), RequestsDestination, socketType),
               new MessageSource<TCommand>(_commandAddress, _deserializerFactory.Create<TCommand, TServiceMessage>(_assemblyFilter, _typeFilter), CommandsDestination, socketType),
               new MessagePublisher<TEvent>(_eventAddress, _serializer, EventsDestination, socketType),
               new MessagePublisher<TResponse>(_responseAddress, _serializer, ResponsesDestination, socketType), 
               typeof(TServiceMessage));
        }

        public override IServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse> CreateEndpointClient()
        {
            const SocketType socketType = SocketType.Client;
            return new ServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse>(
               new MessageSource<TEvent>(_eventAddress, _deserializerFactory.Create<TEvent, TServiceMessage>(_assemblyFilter, _typeFilter), EventsDestination, socketType),
               new MessageSource<TResponse>(_responseAddress, _deserializerFactory.Create<TResponse, TServiceMessage>(_assemblyFilter, _typeFilter), ResponsesDestination, socketType),
               new MessagePublisher<TRequest>(_requestAddress, _serializer, RequestsDestination, socketType),
               new MessagePublisher<TCommand>(_commandAddress, _serializer, CommandsDestination, socketType), 
               typeof(TServiceMessage));
        }
    }
}