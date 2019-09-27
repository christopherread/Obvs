using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Apache.NMS.ActiveMQ;
using Obvs.Configuration;
using Obvs.Serialization;

namespace Obvs.ActiveMQ.Configuration
{
    public interface ICanSpecifyActiveMQServiceName<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TMessage : class
        where TCommand : class, TMessage
        where TEvent : class, TMessage
        where TRequest : class, TMessage
        where TResponse : class, TMessage
    {
        ICanSpecifyActiveMQBroker<TMessage, TCommand, TEvent, TRequest, TResponse> Named(string serviceName);
    }

    public interface ICanSpecifyActiveMQQueue<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TMessage : class
        where TCommand : class, TMessage
        where TEvent : class, TMessage
        where TRequest : class, TMessage
        where TResponse : class, TMessage
    {
        ICanSpecifyActiveMQQueueAcknowledge<TMessage, TCommand, TEvent, TRequest, TResponse> UsingQueueFor<T>() where T : TMessage;
    }

    public interface ICanSpecifyActiveMQQueueAcknowledge<TMessage, TCommand, TEvent, TRequest, TResponse> : ICanSpecifyActiveMQBroker<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TMessage : class
        where TCommand : class, TMessage
        where TEvent : class, TMessage
        where TRequest : class, TMessage
        where TResponse : class, TMessage
    {
        /// <summary>
        /// The message is automatically acknowledged as soon as it is delivered from the broker, Defaults to this.
        /// </summary>
        /// <returns></returns>
        ICanSpecifyActiveMQBroker<TMessage, TCommand, TEvent, TRequest, TResponse> AutoAcknowledge();

        /// <summary>
        /// Manually acknowledges the message after it has successfully deserialized it
        /// </summary>
        /// <returns></returns>
        ICanSpecifyActiveMQBroker<TMessage, TCommand, TEvent, TRequest, TResponse> ClientAcknowledge();
    }

    public interface ICanSpecifyActiveMQBroker<TMessage, TCommand, TEvent, TRequest, TResponse> : 
        ICanSpecifyActiveMQQueue<TMessage, TCommand, TEvent, TRequest, TResponse>,
        ICanSpecifyActiveMQMessageFiltering<TMessage, TCommand, TEvent, TRequest, TResponse>,
        ICanSpecifyEndpointSerializers<TMessage, TCommand, TEvent, TRequest, TResponse>,
        ICanSpecifyActiveMQNoLocalFlag<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TMessage : class
        where TCommand : class, TMessage
        where TEvent : class, TMessage
        where TRequest : class, TMessage
        where TResponse : class, TMessage
    {
        ICanSpecifyActiveMQBrokerCredentials<TMessage, TCommand, TEvent, TRequest, TResponse> ConnectToBroker(string brokerUri);
    }

    public interface ICanSpecifyActiveMQBrokerCredentials<TMessage, TCommand, TEvent, TRequest, TResponse> :
        ICanSpecifyEndpointSerializers<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TMessage : class
        where TCommand : class, TMessage
        where TEvent : class, TMessage
        where TRequest : class, TMessage
        where TResponse : class, TMessage
    {
        ICanSpecifyEndpointSerializers<TMessage, TCommand, TEvent, TRequest, TResponse> WithCredentials(string userName, string password);
    }

    public interface ICanSpecifyActiveMQMessageFiltering<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TMessage : class
        where TCommand : class, TMessage
        where TEvent : class, TMessage
        where TRequest : class, TMessage
        where TResponse : class, TMessage
    {
        ICanSpecifyActiveMQBroker<TMessage, TCommand, TEvent, TRequest, TResponse> FilterReceivedMessages(Func<IDictionary, bool> propertyFilter);
        ICanSpecifyActiveMQBroker<TMessage, TCommand, TEvent, TRequest, TResponse> FilterReceivedMessages(string brokerSelector);
        ICanSpecifyActiveMQBroker<TMessage, TCommand, TEvent, TRequest, TResponse> FilterReceivedMessages(Func<IDictionary, bool> propertyFilter, string brokerSelector);
        ICanSpecifyActiveMQBroker<TMessage, TCommand, TEvent, TRequest, TResponse> AppendMessageProperties(Func<TMessage, Dictionary<string, object>> propertyProvider);
    }

    public interface ICanSpecifyActiveMQNoLocalFlag<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TMessage : class
        where TCommand : class, TMessage
        where TEvent : class, TMessage
        where TRequest : class, TMessage
        where TResponse : class, TMessage
    {
        ICanSpecifyActiveMQBroker<TMessage, TCommand, TEvent, TRequest, TResponse> WithNoLocalFlag();
    }

    internal class ActiveMQFluentConfig<TServiceMessage, TMessage, TCommand, TEvent, TRequest, TResponse> :
        ICanSpecifyActiveMQServiceName<TMessage, TCommand, TEvent, TRequest, TResponse>, 
        ICanCreateEndpointAsClientOrServer<TMessage, TCommand, TEvent, TRequest, TResponse>, 
        ICanSpecifyActiveMQQueueAcknowledge<TMessage, TCommand, TEvent, TRequest, TResponse>,
        ICanSpecifyActiveMQBrokerCredentials<TMessage, TCommand, TEvent, TRequest, TResponse>,
        ICanSpecifyActiveMQNoLocalFlag<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TMessage : class
        where TServiceMessage : class 
        where TCommand : class, TMessage 
        where TEvent : class, TMessage
        where TRequest : class, TMessage 
        where TResponse : class, TMessage
    {
        private readonly ICanAddEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse> _canAddEndpoint;
        private string _serviceName;
        private string _brokerUri;
        private IMessageSerializer _serializer;
        private IMessageDeserializerFactory _deserializerFactory;
        private readonly List<Tuple<Type, AcknowledgementMode>> _queueTypes = new List<Tuple<Type, AcknowledgementMode>>();
        private Func<Assembly, bool> _assemblyFilter;
        private Func<Type, bool> _typeFilter;
        private Func<IDictionary, bool> _propertyFilter;
        private string _brokerSelector;
        private Func<TMessage, Dictionary<string, object>> _propertyProvider;
        private bool _noLocal;
        private string _userName;
        private string _password;
        private Action<ConnectionFactory> _connectionFactoryConfiguration;

        public ActiveMQFluentConfig(ICanAddEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse> canAddEndpoint)
        {
            _canAddEndpoint = canAddEndpoint;
        }

        public ICanSpecifyActiveMQBroker<TMessage, TCommand, TEvent, TRequest, TResponse> Named(string serviceName)
        {
            _serviceName = serviceName;
            return this;
        }

        public ICanCreateEndpointAsClientOrServer<TMessage, TCommand, TEvent, TRequest, TResponse> FilterMessageTypeAssemblies(Func<Assembly, bool> assemblyFilter = null, Func<Type, bool> typeFilter = null)
        {
            _assemblyFilter = assemblyFilter;
            _typeFilter = typeFilter;
            return this;
        }

        public ICanAddEndpointOrLoggingOrCorrelationOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse> AsClient()
        {
            return _canAddEndpoint.WithClientEndpoints(CreateProvider());
        }

        public ICanAddEndpointOrLoggingOrCorrelationOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse> AsServer()
        {
            return _canAddEndpoint.WithServerEndpoints(CreateProvider());
        }

        public ICanAddEndpointOrLoggingOrCorrelationOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse> AsClientAndServer()
        {
            return _canAddEndpoint.WithEndpoints(CreateProvider());
        }

        private ActiveMQServiceEndpointProvider<TServiceMessage, TMessage, TCommand, TEvent, TRequest, TResponse> CreateProvider()
        {
            return new ActiveMQServiceEndpointProvider<TServiceMessage, TMessage, TCommand, TEvent, TRequest, TResponse>(
                _serviceName, _brokerUri, _serializer, _deserializerFactory, _queueTypes, _assemblyFilter, 
                _typeFilter, ActiveMQFluentConfigContext.SharedConnection, _brokerSelector, _propertyFilter,
                _propertyProvider, _userName, _password, _connectionFactoryConfiguration, _noLocal);
        }

        public ICanSpecifyActiveMQBrokerCredentials<TMessage, TCommand, TEvent, TRequest, TResponse> ConnectToBroker(string brokerUri)
        {
            _brokerUri = brokerUri;
            return this;
        }

        public ICanSpecifyEndpointSerializers<TMessage, TCommand, TEvent, TRequest, TResponse> WithCredentials(string userName, string password)
        {
            _userName = userName;
            _password = password;
            return this;
        }

        public ICanCreateEndpointAsClientOrServer<TMessage, TCommand, TEvent, TRequest, TResponse> SerializedWith(IMessageSerializer serializer, IMessageDeserializerFactory deserializerFactory)
        {
            _serializer = serializer;
            _deserializerFactory = deserializerFactory;
            return this;
        }

        public ICanSpecifyActiveMQQueueAcknowledge<TMessage, TCommand, TEvent, TRequest, TResponse> UsingQueueFor<T>() where T : TMessage
        {
            _queueTypes.Add(new Tuple<Type, AcknowledgementMode>(typeof(T), AcknowledgementMode.AutoAcknowledge));
            return this;
        }

        public ICanSpecifyActiveMQBroker<TMessage, TCommand, TEvent, TRequest, TResponse> AutoAcknowledge()
        {
            var existing = _queueTypes.Last();
            _queueTypes.Remove(existing);
            _queueTypes.Add(new Tuple<Type, AcknowledgementMode>(existing.Item1, AcknowledgementMode.AutoAcknowledge));
            return this;
        }

        public ICanSpecifyActiveMQBroker<TMessage, TCommand, TEvent, TRequest, TResponse> ClientAcknowledge()
        {
            var existing = _queueTypes.Last();
            _queueTypes.Remove(existing);
            _queueTypes.Add(new Tuple<Type, AcknowledgementMode>(existing.Item1, AcknowledgementMode.ClientAcknowledge));
            return this;
        }

        public ICanSpecifyActiveMQBroker<TMessage, TCommand, TEvent, TRequest, TResponse> FilterReceivedMessages(Func<IDictionary, bool> propertyFilter)
        {
            _propertyFilter = propertyFilter;
            return this;
        }

        public ICanSpecifyActiveMQBroker<TMessage, TCommand, TEvent, TRequest, TResponse> FilterReceivedMessages(string brokerSelector)
        {
            _brokerSelector = brokerSelector;
            return this;
        }

        public ICanSpecifyActiveMQBroker<TMessage, TCommand, TEvent, TRequest, TResponse> FilterReceivedMessages(Func<IDictionary, bool> propertyFilter, string brokerSelector)
        {
            _propertyFilter = propertyFilter;
            _brokerSelector = brokerSelector;
            return this;
        }

        public ICanSpecifyActiveMQBroker<TMessage, TCommand, TEvent, TRequest, TResponse> AppendMessageProperties(Func<TMessage, Dictionary<string, object>> propertyProvider)
        {
            _propertyProvider = propertyProvider;
            return this;
        }

        public ICanSpecifyActiveMQBroker<TMessage, TCommand, TEvent, TRequest, TResponse> WithConnectionFactoryConfig(Action<ConnectionFactory> connectionFactoryConfiguration)
        {
            _connectionFactoryConfiguration = connectionFactoryConfiguration;
            return this;
        }

        public ICanSpecifyActiveMQBroker<TMessage, TCommand, TEvent, TRequest, TResponse> WithNoLocalFlag()
        {
            _noLocal = true;
            return this;
        }
    }
}