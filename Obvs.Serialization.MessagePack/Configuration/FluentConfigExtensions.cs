using MessagePack;
using Obvs.Configuration;

// v6's fluent configuration, kept for v6 apps and obsolete in v7: its public members are exempt from the rule that every one is documented
#pragma warning disable CS1570, CS1572, CS1591

namespace Obvs.Serialization.MessagePack.Configuration
{
    public static class FluentConfigExtensions
    {
        /// <summary>
        /// Overload prevents having to explicitly reference MessagePack-CSharp in user code.
        /// </summary>
        /// <typeparam name="TMessage"></typeparam>
        /// <typeparam name="TCommand"></typeparam>
        /// <typeparam name="TEvent"></typeparam>
        /// <typeparam name="TRequest"></typeparam>
        /// <typeparam name="TResponse"></typeparam>
        /// <param name="config"></param>
        /// <returns></returns>
        public static ICanCreateEndpointAsClientOrServer<TMessage, TCommand, TEvent, TRequest, TResponse> SerializedAsMessagePackCSharp<TMessage, TCommand, TEvent, TRequest, TResponse>(
            this ICanSpecifyEndpointSerializers<TMessage, TCommand, TEvent, TRequest, TResponse> config)
            where TMessage : class
            where TCommand : class, TMessage
            where TEvent : class, TMessage
            where TRequest : class, TMessage
            where TResponse : class, TMessage
        {
            // ReSharper disable once IntroduceOptionalParameters.Global
            return SerializedAsMessagePackCSharp(config, MessagePackSerializer.DefaultOptions.Resolver);
        }

        public static ICanCreateEndpointAsClientOrServer<TMessage, TCommand, TEvent, TRequest, TResponse> SerializedAsMessagePackCSharp<TMessage, TCommand, TEvent, TRequest, TResponse>(
            this ICanSpecifyEndpointSerializers<TMessage, TCommand, TEvent, TRequest, TResponse> config,
            IFormatterResolver resolver)
            where TMessage : class
            where TCommand : class, TMessage
            where TEvent : class, TMessage
            where TRequest : class, TMessage
            where TResponse : class, TMessage
        {
            return config.SerializedWith(new MessagePackCSharpMessageSerializer(resolver), new MessagePackCSharpMessageDeserializerFactory(resolver));
        }
    }
}