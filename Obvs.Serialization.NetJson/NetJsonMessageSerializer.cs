using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace Obvs.Serialization.NetJson
{
    public class NetJsonMessageSerializer : IMessageSerializer
    {
        private static volatile IDictionary<Type, Action<object, TextWriter>> _streamingSerializers = new Dictionary<Type, Action<object, TextWriter>>();
        private static MethodInfo _serializeMethod;

        static NetJsonMessageSerializer()
        {
            NetJsonDefaults.Set();
        }

        public virtual object Serialize(object message)
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                Serialize(memoryStream, message);

                return memoryStream.ToArray();
            }
        }

        public virtual void Serialize(Stream stream, object message)
        {
            using (var streamWriter = new StreamWriter(stream, NetJsonDefaults.Encoding, 1024, true))
            {
                SerializeCore(streamWriter, message);
            }
        }

        protected void SerializeCore(TextWriter textWriter, object message)
        {
            var streamingSerializer = GetOrAddToStreamingSerializer(message.GetType());
            streamingSerializer(message, textWriter);
        }

        private static Action<object, TextWriter> GetOrAddToStreamingSerializer(Type type)
        {
            Action<object, TextWriter> action;
            if (!_streamingSerializers.TryGetValue(type, out action))
            {
                lock (_streamingSerializers)
                {
                    action = CreateSerializer(type);

                    IDictionary<Type, Action<object, TextWriter>> newCache = new Dictionary<Type, Action<object, TextWriter>>(_streamingSerializers);
                    newCache[type] = action;

                    _streamingSerializers = newCache;
                }
            }

            return action;
        }

        private static MethodInfo GetUnboundedSerializeMethodInfo()
        {
            return _serializeMethod
                   ?? (_serializeMethod =
                       typeof(NetJSON.NetJSON).GetMethods(BindingFlags.Static | BindingFlags.Public)
                           .Single(
                               mi => // This matches NetJSON.NetJSON.Serialize<T>(T message, TextWriter writer) method.
                                   mi.Name == nameof(NetJSON.NetJSON.Serialize)
                                   && mi.IsGenericMethodDefinition
                                   && mi.GetParameters().Length == 2));
        }

        private static Action<object, TextWriter> CreateSerializer(Type type)
        {
            var methodInfo = GetUnboundedSerializeMethodInfo().MakeGenericMethod(type);

            DynamicMethod shim = new DynamicMethod("SerializeWithType", typeof(void), new[] { typeof(object), typeof(TextWriter) }, typeof(NetJsonMessageSerializer));
            ILGenerator il = shim.GetILGenerator();

            il.Emit(OpCodes.Ldarg_0); // Load message (object)

            il.Emit(OpCodes.Ldarg_1); // Load textWriter

            il.Emit(OpCodes.Call, methodInfo); // Invoke Serialize<T>(msg, textwriter) method

            il.Emit(OpCodes.Ret); // void return

            return (Action<object, TextWriter>)shim.CreateDelegate(typeof(Action<object, TextWriter>));
        }

        //public void Serialize(Stream stream, object message)
        //{
        //    using (TextWriter writer = new StreamWriter(stream))
        //    {
        //        writer.Write(Serialize(message));

        //        writer.Flush();
        //    }
        //}
    }
}