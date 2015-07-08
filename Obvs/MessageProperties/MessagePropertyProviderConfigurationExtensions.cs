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
        public static ICanSpecifyEndpointSerializers<TMessage, TCommand, TEvent, TRequest, TResponse> UsingMessagePropertyProviderFor<TMessage, TCommand, TEvent, TRequest, TResponse, T>(this ICanSpecifyPropertyProviders<TMessage, TCommand, TEvent, TRequest, TResponse> configuration, Func<T, IEnumerable<KeyValuePair<string, object>>> providerFunc) 
            where TMessage : class
            where TCommand : class, TMessage
            where TEvent : class, TMessage
            where TRequest : class, TMessage
            where TResponse : class, TMessage
            where T : class, TMessage
        {
            if(providerFunc == null) throw new ArgumentNullException("providerFunc");

            return configuration.UsingMessagePropertyProviderFor<T>(new FuncMessagePropertyProvider<T>(providerFunc));
        }

        public static ICanSpecifyEndpointSerializers<TMessage, TCommand, TEvent, TRequest, TResponse> UsingMessagePropertyProviderFor<TMessage, TCommand, TEvent, TRequest, TResponse, T>(this ICanSpecifyPropertyProviders<TMessage, TCommand, TEvent, TRequest, TResponse> configuration, Func<T, KeyValuePair<string, object>> providerFunc)
            where TMessage : class
            where TCommand : class, TMessage
            where TEvent : class, TMessage
            where TRequest : class, TMessage
            where TResponse : class, TMessage
            where T : class, TMessage
        {
            if(providerFunc == null) throw new ArgumentNullException("providerFunc");

            return configuration.UsingMessagePropertyProviderFor<T>(new FuncMessagePropertyProvider<T>(providerFunc));
        }

        public static ICanSpecifyEndpointSerializers<TMessage, TCommand, TEvent, TRequest, TResponse> UsingMessagePropertyProvidersFor<TMessage, TCommand, TEvent, TRequest, TResponse, T>(this ICanSpecifyPropertyProviders<TMessage, TCommand, TEvent, TRequest, TResponse> configuration, IEnumerable<IMessagePropertyProvider<T>> providers)
            where TMessage : class
            where TCommand : class, TMessage
            where TEvent : class, TMessage
            where TRequest : class, TMessage
            where TResponse : class, TMessage
            where T : class, TMessage
        {
            if(providers == null) throw new ArgumentNullException("providers");

            return configuration.UsingMessagePropertyProviderFor<T>(new CompositeMessagePropertyProvider<T>(providers));
        }

        public static ICanSpecifyEndpointSerializers<TMessage, TCommand, TEvent, TRequest, TResponse> UsingMessagePropertyProvidersFor<TMessage, TCommand, TEvent, TRequest, TResponse, T>(this ICanSpecifyPropertyProviders<TMessage, TCommand, TEvent, TRequest, TResponse> configuration, params Func<T, KeyValuePair<string, object>>[] providers)
            where TMessage : class
            where TCommand : class, TMessage
            where TEvent : class, TMessage
            where TRequest : class, TMessage
            where TResponse : class, TMessage
            where T : class, TMessage
        {
            if(providers == null) throw new ArgumentNullException("providers");

            return configuration.UsingMessagePropertyProvidersFor<TMessage, TCommand, TEvent, TRequest, TResponse, T>(providers.Select(p => new FuncMessagePropertyProvider<T>(p)));
        }
    }
}
