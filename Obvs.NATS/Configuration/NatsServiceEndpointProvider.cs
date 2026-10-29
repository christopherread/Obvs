using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reflection;
using NATS.Client.Core;
using Obvs.Configuration;
using Obvs.Serialization;

// v6's NATS transport building blocks, which UseNats(...) is built on: its public members are exempt from the rule that every one is documented
#pragma warning disable CS1570, CS1572, CS1591

namespace Obvs.NATS.Configuration
{
    public class NatsServiceEndpointProvider<TServiceMessage, TMessage, TCommand, TEvent, TRequest, TResponse> : ServiceEndpointProviderBase<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TMessage : class
        where TServiceMessage : class
        where TCommand : class, TMessage
        where TEvent : class, TMessage
        where TRequest : class, TMessage
        where TResponse : class, TMessage
    {
        private readonly IMessageSerializer _serializer;
        private readonly IMessageDeserializerFactory _deserializerFactory;
        
        private readonly Func<Assembly, bool> _assemblyFilter;
        private readonly Func<Type, bool> _typeFilter;
        
        private readonly Func<IDictionary<string, string>, bool> _propertyFilter;
        private readonly Func<TMessage, Dictionary<string, string>> _propertyProvider;
        private readonly INatsConnection _connection;

        public NatsServiceEndpointProvider(NatsEndpointSettings<TMessage> settings, IMessageSerializer serializer, IMessageDeserializerFactory deserializerFactory, Func<Assembly, bool> assemblyFilter = null, Func<Type, bool> typeFilter = null, Func<IDictionary<string, string>, bool> propertyFilter = null, Func<TMessage, Dictionary<string, string>> propertyProvider = null)
            : base(settings.ServiceName)
        {
            _serializer = serializer;
            _deserializerFactory = deserializerFactory;
            _assemblyFilter = assemblyFilter;
            _typeFilter = typeFilter;
            _propertyFilter = propertyFilter;
            _propertyProvider = propertyProvider;

            if (string.IsNullOrEmpty(settings.Connection?.Url))
            {
                throw new InvalidOperationException(string.Format("For service endpoint '{0}', please specify a broker URL to connect to.", settings.ServiceName));
            }

            _connection = SharedConnectionFactory.Get(settings.Connection.Url, settings.Connection.IsShared);
        }

        public override IServiceEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse> CreateEndpoint()
        {
            return new DisposingServiceEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse>(
                new ServiceEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse>(
                    CreateSource<TRequest>(_connection, RequestsDestination),
                    CreateSource<TCommand>(_connection, CommandsDestination),
                    CreatePublisher<TEvent>(_connection, EventsDestination),
                    CreatePublisher<TResponse>(_connection, ResponsesDestination),
                    typeof(TServiceMessage)),
                GetConnectionDisposable(_connection));
        }

        private IMessageSource<T> CreateSource<T>(INatsConnection connection, string subject) where T : class, TMessage
        {
            return new MessageSource<T>(connection, subject,
                _deserializerFactory.Create<T, TServiceMessage>(_assemblyFilter, _typeFilter), 
                _propertyFilter);
        }

        private IMessagePublisher<T> CreatePublisher<T>(INatsConnection connection, string destination) where T : class, TMessage
        {
           return new MessagePublisher<T>(connection, destination, _serializer, _propertyProvider);
        }
        
        public override IServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse> CreateEndpointClient()
        {
            return new DisposingServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse>(
                new ServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse>(
                    CreateSource<TEvent>(_connection, EventsDestination),
                    CreateSource<TResponse>(_connection, ResponsesDestination),
                    CreatePublisher<TRequest>(_connection, RequestsDestination),
                    CreatePublisher<TCommand>(_connection, CommandsDestination),
                    typeof(TServiceMessage)),
                GetConnectionDisposable(_connection));
        }
        
        private static IDisposable GetConnectionDisposable(INatsConnection connection)
        {
#pragma warning disable VSTHRD002 // IDisposable.Dispose is synchronous
            return Disposable.Create(() => connection.DisposeAsync().AsTask().GetAwaiter().GetResult());
#pragma warning restore VSTHRD002
        }
    }
}