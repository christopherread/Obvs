using Obvs.Configuration;
using Obvs.Types;

namespace Obvs.NetMQ.Configuration
{
    public static class NetMqConfigExtensions
    {
        public static ICanAddNetMqServiceName<TMessage, TCommand, TEvent, TRequest, TResponse> WithNetMqEndpoints<TServiceMessage, TMessage, TCommand, TEvent, TRequest, TResponse>(this ICanAddEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse> canAddEndpoint) 
            where TMessage : class
            where TServiceMessage : class, TMessage
            where TEvent : class, TMessage
            where TRequest : class, TMessage
            where TResponse : class, TMessage
            where TCommand : class, TMessage
        {
            return new NetMqFluentConfig<TServiceMessage, TMessage, TCommand, TEvent, TRequest, TResponse>(canAddEndpoint);
        }

        public static ICanAddNetMqServiceName<IMessage, ICommand, IEvent, IRequest, IResponse> WithNetMqEndpoints<TServiceMessage>(this ICanAddEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse> canAddEndpoint)
            where TServiceMessage : class, IMessage
        {
            return new NetMqFluentConfig<TServiceMessage, IMessage, ICommand, IEvent, IRequest, IResponse>(canAddEndpoint);
        }
    }
}