using System;
using Obvs.Configuration;

// v6's NATS transport building blocks, public for v6's fluent configuration; v7 apps use UseNats(...): its public members are exempt from the rule that every one is documented
#pragma warning disable CS1570, CS1572, CS1591

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