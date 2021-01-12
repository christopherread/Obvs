using Obvs.Configuration;
using Obvs.Types;

namespace Obvs.RabbitMQ.Configuration
{
    public static class RabbitMQConfigExtensions
    {
        public static ICanSpecifyRabbitMQServiceName<TMessage, TCommand, TEvent, TRequest, TResponse> WithRabbitMQEndpoints<TServiceMessage, TMessage, TCommand, TEvent, TRequest, TResponse>(this ICanAddEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse> canAddEndpoint) 
            where TServiceMessage : class
            where TMessage : class
            where TCommand : class, TMessage 
            where TEvent : class, TMessage 
            where TRequest : class, TMessage 
            where TResponse : class, TMessage
        {
            return new RabbitMQFluentConfig<TServiceMessage, TMessage, TCommand, TEvent, TRequest, TResponse>(canAddEndpoint);
        }
        
        public static ICanSpecifyRabbitMQServiceName<IMessage, ICommand, IEvent, IRequest, IResponse> WithRabbitMQEndpoints<TServiceMessage>(this ICanAddEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse> canAddEndpoint) 
            where TServiceMessage : class
        {
            return new RabbitMQFluentConfig<TServiceMessage, IMessage, ICommand, IEvent, IRequest, IResponse>(canAddEndpoint);
        }
    }
}