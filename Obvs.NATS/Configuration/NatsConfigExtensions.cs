using System;
using Obvs.Configuration;

namespace Obvs.NATS.Configuration
{
    public static class NatsConfigExtensions
    {
        public static ICanSpecifyEndpointSerializers<TMessage, TCommand, TEvent, TRequest, TResponse>
            WithNatsEndpoint<TServiceMessage, TMessage, TCommand, TEvent, TRequest, TResponse>(this ICanAddEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse> canAddEndpoint,
                Action<NatsEndpointSettings<TMessage>> setSettings)
            where TMessage : class
            where TServiceMessage : class
            where TCommand : class, TMessage
            where TEvent : class, TMessage
            where TRequest : class, TMessage
            where TResponse : class, TMessage
        {
            var settings = new NatsEndpointSettings<TMessage>();
            setSettings(settings);
            return new NatsFluentConfig<TServiceMessage, TMessage, TCommand, TEvent, TRequest, TResponse>(canAddEndpoint, settings);
        }
    }
}