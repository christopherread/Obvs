using System;
using System.Collections.Generic;
using System.Reflection;
using Obvs.Types;

namespace Obvs.MessageProperties
{
    public sealed class MessagePropertyProviderManager<TMessage> where TMessage : class
    {
        private IDictionary<Type, List<object>> _messagePropertyProviders = new Dictionary<Type, List<object>>();

        public MessagePropertyProviderManager()
        {
            // Always include the default provider right now since serializer implementations depend on it being there
            Add(new DefaultPropertyProvider<TMessage>());
        }

        public void Add<T>(IMessagePropertyProvider<T> messagePropertyProvider) where T : class, TMessage
        {
            List<object> propertyProvidersForType;
            Type messageType = typeof(T);

            if (!_messagePropertyProviders.TryGetValue(messageType, out propertyProvidersForType))
            {
                propertyProvidersForType = new List<object>();

                _messagePropertyProviders.Add(messageType, propertyProvidersForType);
            }

            propertyProvidersForType.Add(messagePropertyProvider);
        }

        public IMessagePropertyProvider<T> GetMessagePropertyProviderFor<T>() where T : class, TMessage
        {
            return new DispatchingPropertyProvider<T>(_messagePropertyProviders);
        }
    }

    internal sealed class DispatchingPropertyProvider<TMessage> : IMessagePropertyProvider<TMessage>
        where TMessage : class
    {
        private readonly TypeInfo _baseMessageType = typeof(TMessage).GetTypeInfo();
        private readonly IDictionary<Type, List<object>> _messagePropertyProviders;

        public DispatchingPropertyProvider(IDictionary<Type, List<object>> messagePropertyProviders)
        {
            _messagePropertyProviders = messagePropertyProviders;
        }

        #region IMessagePropertyProvider<TMessage> Members

        public IEnumerable<KeyValuePair<string, object>> GetProperties(TMessage message)
        {
            object[] getPropertiesMethodParameters = new object[] {message};

            foreach (TypeInfo typeInfo in FindAllApplicableTypesForMessage(message))
            {
                var type = typeInfo.AsType();

                List<object> providersForType;
                if (_messagePropertyProviders.TryGetValue(type, out providersForType))
                {
                    MethodInfo getPropertiesMethodInfo = typeof(IMessagePropertyProvider<>).MakeGenericType(type)
                        .GetRuntimeMethod("GetProperties", new[] {type});

                    foreach (object providerForType in providersForType)
                    {
                        IEnumerable<KeyValuePair<string, object>> properties =
                            (IEnumerable<KeyValuePair<string, object>>) getPropertiesMethodInfo.Invoke(providerForType,
                                getPropertiesMethodParameters);

                        foreach (KeyValuePair<string, object> property in properties)
                        {
                            yield return property;
                        }
                    }
                }
            }
        }

        private IEnumerable<TypeInfo> FindAllApplicableTypesForMessage(TMessage message)
        {
            var messageTypeInfo = message.GetType().GetTypeInfo();

            yield return messageTypeInfo;

            var nextBaseType = messageTypeInfo.BaseType.GetTypeInfo();

            while (!Equals(nextBaseType, typeof(object).GetTypeInfo()))
            {
                yield return nextBaseType;

                nextBaseType = nextBaseType.BaseType.GetTypeInfo();
            }

            if (_baseMessageType.IsInterface)
            {
                foreach (Type messageTypeInterface in messageTypeInfo.FindInterfaces(
                    (t, fc) => _baseMessageType.IsAssignableFrom(t), null))
                {
                    yield return messageTypeInterface.GetTypeInfo();
                }
            }
        }

        #endregion
    }
}