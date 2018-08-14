using System;
using Apache.NMS;
using Apache.NMS.ActiveMQ;
using Obvs.Configuration;
using Obvs.Types;
using IMessage = Obvs.Types.IMessage;

namespace Obvs.ActiveMQ.Configuration
{
    public static class ActiveMQConfigExtensions
    {
        public static ICanSpecifyActiveMQServiceName<TMessage, TCommand, TEvent, TRequest, TResponse>
            WithActiveMQEndpoints<TServiceMessage, TMessage, TCommand, TEvent, TRequest, TResponse>(this ICanAddEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse> canAddEndpoint)
            where TMessage : class
            where TServiceMessage : class
            where TCommand : class, TMessage
            where TEvent : class, TMessage
            where TRequest : class, TMessage
            where TResponse : class, TMessage
        {
            return new ActiveMQFluentConfig<TServiceMessage, TMessage, TCommand, TEvent, TRequest, TResponse>(canAddEndpoint);
        }

        public static ICanSpecifyEndpointSerializers<TMessage, TCommand, TEvent, TRequest, TResponse>
            WithActiveMQEndpoint<TServiceMessage, TMessage, TCommand, TEvent, TRequest, TResponse>(
            this ICanAddEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse> canAddEndpoint,
            Func<ICanAddEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse>, ICanSpecifyEndpointSerializers<TMessage, TCommand, TEvent, TRequest, TResponse>> endPointFactory)
            where TMessage : class
            where TServiceMessage : class
            where TCommand : class, TMessage
            where TEvent : class, TMessage
            where TRequest : class, TMessage
            where TResponse : class, TMessage
        {
            return endPointFactory(canAddEndpoint);
        }

        public static ICanSpecifyActiveMQServiceName<IMessage, ICommand, IEvent, IRequest, IResponse> WithActiveMQEndpoints<TServiceMessage>(this ICanAddEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse> canAddEndpoint) where TServiceMessage : class
        {
            return new ActiveMQFluentConfig<TServiceMessage, IMessage, ICommand, IEvent, IRequest, IResponse>(canAddEndpoint);
        }

        public static ICanAddEndpointOrLoggingOrCorrelationOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse> WithActiveMQSharedConnectionScope<TMessage, TCommand, TEvent, TRequest, TResponse>(
            this ICanAddEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse> canAddEndpoint,
            string brokerUri,
            Func<ICanAddEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse>, ICanAddEndpointOrLoggingOrCorrelationOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse>> endPointFactory)
            where TMessage : class
            where TCommand : class, TMessage
            where TEvent : class, TMessage
            where TRequest : class, TMessage
            where TResponse : class, TMessage
        {
            return canAddEndpoint.WithActiveMQSharedConnectionScope(brokerUri, null, null, cf => { }, endPointFactory);
        }

        public static ICanAddEndpointOrLoggingOrCorrelationOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse>
            WithActiveMQSharedConnectionScope<TMessage, TCommand, TEvent, TRequest, TResponse>(
                this ICanAddEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse> canAddEndpoint,
                string brokerUri, string userName, string password,
                Func<ICanAddEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse>,
                        ICanAddEndpointOrLoggingOrCorrelationOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse>>
                    endPointFactory)
            where TMessage : class
            where TCommand : class, TMessage
            where TEvent : class, TMessage
            where TRequest : class, TMessage
            where TResponse : class, TMessage
        {
            return canAddEndpoint.WithActiveMQSharedConnectionScope(brokerUri, userName, password, cf => { }, endPointFactory);
        }

        public static ICanAddEndpointOrLoggingOrCorrelationOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse> WithActiveMQSharedConnectionScope<TMessage, TCommand, TEvent, TRequest, TResponse>(
            this ICanAddEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse> canAddEndpoint,
            string brokerUri, string userName, string password,
            Action<ConnectionFactory> connectionFactoryConfig,
            Func<ICanAddEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse>, ICanAddEndpointOrLoggingOrCorrelationOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse>> endPointFactory)
            where TMessage : class
            where TCommand : class, TMessage
            where TEvent : class, TMessage
            where TRequest : class, TMessage
            where TResponse : class, TMessage
        {
            var connectionFactory = new ConnectionFactory(brokerUri, ConnectionClientId.CreateWithSuffix("Shared"))
            {
                CopyMessageOnSend = false // We never reuse our messages so don't need to clone when sending
            };

            connectionFactoryConfig(connectionFactory);

            var connection = !string.IsNullOrEmpty(userName)
                ? connectionFactory.CreateConnection(userName, password)
                : connectionFactory.CreateConnection();
            ActiveMQFluentConfigContext.SharedConnection = connection.GetLazyConnection();

            var result = endPointFactory(canAddEndpoint);

            ActiveMQFluentConfigContext.SharedConnection = null;
            return result;
        }

        public static ICanAddEndpointOrLoggingOrCorrelationOrCreate<IMessage, ICommand, IEvent, IRequest, IResponse> WithActiveMQSharedConnectionScope<TServiceMessage>(
            this ICanAddEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse> canAddEndpoint,
            string brokerUri,
            Action<ConnectionFactory> connectionFactoryConfig,
            Func<ICanAddEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse>, ICanAddEndpointOrLoggingOrCorrelationOrCreate<IMessage, ICommand, IEvent, IRequest, IResponse>> endPointFactory) where TServiceMessage : class
        {
            return canAddEndpoint.WithActiveMQSharedConnectionScope(brokerUri, null, null, connectionFactoryConfig, endPointFactory);
        }
        public static ICanAddEndpointOrLoggingOrCorrelationOrCreate<IMessage, ICommand, IEvent, IRequest, IResponse> WithActiveMQSharedConnectionScope<TServiceMessage>(
            this ICanAddEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse> canAddEndpoint,
            string brokerUri,
            Func<ICanAddEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse>, ICanAddEndpointOrLoggingOrCorrelationOrCreate<IMessage, ICommand, IEvent, IRequest, IResponse>> endPointFactory) where TServiceMessage : class
        {
            return canAddEndpoint.WithActiveMQSharedConnectionScope(brokerUri, endPointFactory);
        }

        public static ICanAddEndpointOrLoggingOrCorrelationOrCreate<IMessage, ICommand, IEvent, IRequest, IResponse> WithActiveMQSharedConnectionScope<TServiceMessage>(
            this ICanAddEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse> canAddEndpoint,
            string brokerUri, string userName, string password,
            Func<ICanAddEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse>, ICanAddEndpointOrLoggingOrCorrelationOrCreate<IMessage, ICommand, IEvent, IRequest, IResponse>> endPointFactory) where TServiceMessage : class
        {
            return canAddEndpoint.WithActiveMQSharedConnectionScope(brokerUri, userName, password, endPointFactory);
        }
        public static ICanAddEndpointOrLoggingOrCorrelationOrCreate<IMessage, ICommand, IEvent, IRequest, IResponse> WithActiveMQSharedConnectionScope<TServiceMessage>(
            this ICanAddEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse> canAddEndpoint,
            string brokerUri, string userName, string password,
            Action<ConnectionFactory> connectionFactoryConfig,
            Func<ICanAddEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse>, ICanAddEndpointOrLoggingOrCorrelationOrCreate<IMessage, ICommand, IEvent, IRequest, IResponse>> endPointFactory) where TServiceMessage : class
        {
            return canAddEndpoint.WithActiveMQSharedConnectionScope(brokerUri, userName, password, connectionFactoryConfig, endPointFactory);
        }
    }
}