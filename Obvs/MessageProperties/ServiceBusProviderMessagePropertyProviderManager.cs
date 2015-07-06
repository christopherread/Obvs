using System;
using System.Collections.Generic;
using System.Reflection;
using Obvs.Types;

namespace Obvs.MessageProperties
{
    public sealed class MessagePropertyProviderManager
    {
        private IDictionary<Type, List<object>> _messagePropertyProviders = new Dictionary<Type, List<object>>();

        public void Add<TMessage>(IMessagePropertyProvider<TMessage> messagePropertyProvider) where TMessage : IMessage
        {
            List<object> propertyProvidersForType;
            Type messageType = typeof(TMessage);

            if(!_messagePropertyProviders.TryGetValue(messageType, out propertyProvidersForType))
            {
                propertyProvidersForType = new List<object>();

                _messagePropertyProviders.Add(messageType, propertyProvidersForType);
            }

            propertyProvidersForType.Add(messagePropertyProvider);
        }

        public IMessagePropertyProvider<TMessage> GetMessagePropertyProviderFor<TMessage>() where TMessage : IMessage
        {
            return new DispatchingPropertyProvider<TMessage>(_messagePropertyProviders);
        }
    }

    internal sealed class DispatchingPropertyProvider<TMessage> : IMessagePropertyProvider<TMessage> where TMessage : IMessage
    {
        private IDictionary<Type, List<object>> _messagePropertyProviders;

        public DispatchingPropertyProvider(IDictionary<Type, List<object>> messagePropertyProviders)
        {
            _messagePropertyProviders = messagePropertyProviders;
        }

        #region IMessagePropertyProvider<TMessage> Members

        public IEnumerable<KeyValuePair<string, object>> GetProperties(TMessage message)
        {
            object[] getPropertiesMethodParameters = new object[] { message };

            foreach(Type type in FindAllApplicableTypesForMessage(message))
            {
                List<object> providersForType;

                if(_messagePropertyProviders.TryGetValue(type, out providersForType))
                {
                    MethodInfo getPropertiesMethodInfo = typeof(IMessagePropertyProvider<>).MakeGenericType(type).GetMethod("GetProperties");

                    foreach(object providerForType in providersForType)
                    {
                        IEnumerable<KeyValuePair<string, object>> properties = (IEnumerable<KeyValuePair<string, object>>)getPropertiesMethodInfo.Invoke(providerForType, getPropertiesMethodParameters);

                        foreach(KeyValuePair<string, object> property in properties)
                        {
                            yield return property;
                        }
                    }
                }
            }
        }

        private static IEnumerable<Type> FindAllApplicableTypesForMessage<TMessage>(TMessage message)
        {
            Type messageType = message.GetType();

            yield return messageType;

            if(messageType.IsClass)
            {
                Type nextBaseType = messageType.BaseType;

                while(nextBaseType != typeof(object))
                {
                    yield return nextBaseType;

                    nextBaseType = nextBaseType.BaseType;
                }
            }

            foreach(Type messageTypeInterface in messageType.FindInterfaces((t, fc) => typeof(IMessage).IsAssignableFrom(t), null))
            {
                yield return messageTypeInterface;
            }
        }

        #endregion
    }
}
