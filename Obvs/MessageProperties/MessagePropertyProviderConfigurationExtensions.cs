using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Obvs.MessageProperties;
using Obvs.Types;

namespace Obvs.Configuration
{
    public static class MessagePropertyProviderConfigurationExtensions
    {
        public static ICanSpecifyEndpointSerializers<TMessage, TCommand, TEvent, TRequest, TResponse> UsingMessagePropertyProviderFor<TMessageWithProperties, TMessage, TCommand, TEvent, TRequest, TResponse>(this ICanSpecifyPropertyProviders<TMessage, TCommand, TEvent, TRequest, TResponse> configuration, Func<TMessageWithProperties, IEnumerable<KeyValuePair<string, object>>> providerFunc) 
            where TMessage : class
            where TCommand : class, TMessage
            where TEvent : class, TMessage
            where TRequest : class, TMessage
            where TResponse : class, TMessage
            where TMessageWithProperties : class, TMessage
        {
            if(providerFunc == null) throw new ArgumentNullException("providerFunc");

            return configuration.UsingMessagePropertyProviderFor<TMessageWithProperties, TMessage, TCommand, TEvent, TRequest, TResponse>(new FuncMessagePropertyProvider<TMessageWithProperties>(providerFunc));
        }

        public static ICanSpecifyEndpointSerializers<TMessage, TCommand, TEvent, TRequest, TResponse> UsingMessagePropertyProviderFor<TMessageWithProperties, TMessage, TCommand, TEvent, TRequest, TResponse>(this ICanSpecifyPropertyProviders<TMessage, TCommand, TEvent, TRequest, TResponse> configuration, Func<TMessageWithProperties, KeyValuePair<string, object>> providerFunc)
            where TMessage : class
            where TCommand : class, TMessage
            where TEvent : class, TMessage
            where TRequest : class, TMessage
            where TResponse : class, TMessage
            where TMessageWithProperties : class, TMessage
        {
            if(providerFunc == null) throw new ArgumentNullException("providerFunc");

            return configuration.UsingMessagePropertyProviderFor<TMessageWithProperties, TMessage, TCommand, TEvent, TRequest, TResponse>(new FuncMessagePropertyProvider<TMessageWithProperties>(providerFunc));
        }

        public static ICanSpecifyEndpointSerializers<TMessage, TCommand, TEvent, TRequest, TResponse> UsingMessagePropertyProvidersFor<TMessageWithProperties, TMessage, TCommand, TEvent, TRequest, TResponse>(this ICanSpecifyPropertyProviders<TMessage, TCommand, TEvent, TRequest, TResponse> configuration, IEnumerable<IMessagePropertyProvider<TMessageWithProperties>> providers)
            where TMessage : class
            where TCommand : class, TMessage
            where TEvent : class, TMessage
            where TRequest : class, TMessage
            where TResponse : class, TMessage
            where TMessageWithProperties : class, TMessage
        {
            if(providers == null) throw new ArgumentNullException("providers");

            return configuration.UsingMessagePropertyProviderFor<TMessageWithProperties, TMessage, TCommand, TEvent, TRequest, TResponse>(new CompositeMessagePropertyProvider<TMessageWithProperties>(providers));
        }

        public static ICanSpecifyEndpointSerializers<TMessage, TCommand, TEvent, TRequest, TResponse> UsingMessagePropertyProvidersFor<TMessageWithProperties, TMessage, TCommand, TEvent, TRequest, TResponse>(this ICanSpecifyPropertyProviders<TMessage, TCommand, TEvent, TRequest, TResponse> configuration, params Func<TMessageWithProperties, KeyValuePair<string, object>>[] providers)
            where TMessage : class
            where TCommand : class, TMessage
            where TEvent : class, TMessage
            where TRequest : class, TMessage
            where TResponse : class, TMessage
            where TMessageWithProperties : class, TMessage
        {
            if(providers == null) throw new ArgumentNullException("providers");

            return configuration.UsingMessagePropertyProvidersFor<TMessageWithProperties, TMessage, TCommand, TEvent, TRequest, TResponse>(providers.Select(p => new FuncMessagePropertyProvider<TMessageWithProperties>(p)));
        }
    }
}
