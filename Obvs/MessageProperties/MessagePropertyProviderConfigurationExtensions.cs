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
        public static ICanSpecifyEndpointSerializers UsingMessagePropertyProviderFor<TMessage>(this ICanSpecifyPropertyProviders configuration, Func<TMessage, IEnumerable<KeyValuePair<string, object>>> providerFunc) where TMessage : IMessage
        {
            if(providerFunc == null) throw new ArgumentNullException("providerFunc");
            
            return configuration.UsingMessagePropertyProviderFor<TMessage>(new FuncMessagePropertyProvider<TMessage>(providerFunc));
        }

        public static ICanSpecifyEndpointSerializers UsingMessagePropertyProviderFor<TMessage>(this ICanSpecifyPropertyProviders configuration, Func<TMessage, KeyValuePair<string, object>> providerFunc) where TMessage : IMessage
        {
            if(providerFunc == null) throw new ArgumentNullException("providerFunc");
            
            return configuration.UsingMessagePropertyProviderFor<TMessage>(new FuncMessagePropertyProvider<TMessage>(providerFunc));
        }

        public static ICanSpecifyEndpointSerializers UsingMessagePropertyProvidersFor<TMessage>(this ICanSpecifyPropertyProviders configuration, IEnumerable<IMessagePropertyProvider<TMessage>> providers) where TMessage : IMessage
        {
            if(providers == null) throw new ArgumentNullException("providers");

            return configuration.UsingMessagePropertyProviderFor<TMessage>(new CompositeMessagePropertyProvider<TMessage>(providers));
        }

        public static ICanSpecifyEndpointSerializers UsingMessagePropertyProvidersFor<TMessage>(this ICanSpecifyPropertyProviders configuration, params Func<TMessage, KeyValuePair<string, object>>[] providers) where TMessage : IMessage
        {
            if(providers == null) throw new ArgumentNullException("providers");

            return configuration.UsingMessagePropertyProvidersFor<TMessage>(providers.Select(p => new FuncMessagePropertyProvider<TMessage>(p)));
        }
    }
}
