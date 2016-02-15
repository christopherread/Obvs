using System;
using System.Reflection;
using NetMQ;
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
        private readonly NetMQContext _context = NetMQContext.Create();
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
            return new ServiceEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse>(
               new MessageSource<TRequest>(_requestAddress, _deserializerFactory.Create<TRequest, TServiceMessage>(_assemblyFilter, _typeFilter), _context, RequestsDestination),
               new MessageSource<TCommand>(_commandAddress, _deserializerFactory.Create<TCommand, TServiceMessage>(_assemblyFilter, _typeFilter), _context, CommandsDestination),
               new MessagePublisher<TEvent>(_eventAddress, _serializer, _context, EventsDestination),
               new MessagePublisher<TResponse>(_responseAddress, _serializer, _context, ResponsesDestination), 
               typeof(TServiceMessage));
        }

        public override IServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse> CreateEndpointClient()
        {
            return new ServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse>(
               new MessageSource<TEvent>(_eventAddress, _deserializerFactory.Create<TEvent, TServiceMessage>(_assemblyFilter, _typeFilter), _context, EventsDestination),
               new MessageSource<TResponse>(_responseAddress, _deserializerFactory.Create<TResponse, TServiceMessage>(_assemblyFilter, _typeFilter), _context, ResponsesDestination),
               new MessagePublisher<TRequest>(_requestAddress, _serializer, _context, RequestsDestination),
               new MessagePublisher<TCommand>(_commandAddress, _serializer, _context, CommandsDestination), 
               typeof(TServiceMessage));
        }
    }
}