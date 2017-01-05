using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Obvs.Configuration;
using ProtoBuf.Meta;

namespace Obvs.Serialization.ProtoBuf
{
    public class ProtoBufMessageDeserializerFactory : IMessageDeserializerFactory
    {
        private readonly RuntimeTypeModel _model;

        public ProtoBufMessageDeserializerFactory(RuntimeTypeModel model = null)
        {
            _model = model;
        }
        public IEnumerable<IMessageDeserializer<TMessage>> Create<TMessage, TServiceMessage>(Func<Assembly, bool> assemblyFilter = null, Func<Type, bool> typeFilter = null)
            where TMessage : class
            where TServiceMessage : class
        {
            return MessageTypes.Get<TMessage, TServiceMessage>(assemblyFilter, typeFilter)
                .Select(type => typeof(ProtoBufMessageDeserializer<>).MakeGenericType(type))
                .Select(deserializerGeneric => Activator.CreateInstance(deserializerGeneric, _model) as IMessageDeserializer<TMessage>)
                .ToArray();
        }
    }
}