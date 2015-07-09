using System;
using System.Collections.Generic;
using System.Linq;
using Obvs.Types;

namespace Obvs.MessageProperties
{
    public class CompositeMessagePropertyProvider<TMessage> : IMessagePropertyProvider<TMessage> where TMessage : class
    {
        List<IMessagePropertyProvider<TMessage>> _providers;

        public CompositeMessagePropertyProvider()
        {
            _providers = new List<IMessagePropertyProvider<TMessage>>();
        }

        public CompositeMessagePropertyProvider(IEnumerable<IMessagePropertyProvider<TMessage>> providers)
        {
            _providers = new List<IMessagePropertyProvider<TMessage>>(providers);
        }

        public IEnumerable<KeyValuePair<string, object>> GetProperties(TMessage message)
        {
            return _providers.SelectMany(p => p.GetProperties(message));
        }

        public List<IMessagePropertyProvider<TMessage>> Providers
        {
            get
            {
                return _providers;
            }
        }
    }

    public static class MessagePropertyProviderListExtensions
    {
        public static void Add<T>(this List<IMessagePropertyProvider<T>> propertyProviderList, Func<T, IEnumerable<KeyValuePair<string, object>>> providerFunc) where T : class
        {
            propertyProviderList.Add(new FuncMessagePropertyProvider<T>(providerFunc));
        }

        public static void Add<T>(this List<IMessagePropertyProvider<T>> propertyProviderList, Func<T, KeyValuePair<string, object>> providerFunc) where T : class
        {
            propertyProviderList.Add(new FuncMessagePropertyProvider<T>(providerFunc));
        }

        public static void AddRange<T>(this List<IMessagePropertyProvider<T>> propertyProviderList, params Func<T, KeyValuePair<string, object>>[] providerFuncs) where T : class
        {
            propertyProviderList.AddRange(providerFuncs.Select(pf => new FuncMessagePropertyProvider<T>(pf)));
        }
    }
}
