using System;
using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using Apache.NMS;
using Apache.NMS.Util;

namespace Obvs.ActiveMQ.Utils
{
    internal static class PrimitiveMapUtils
    { 
        private static readonly Lazy<Func<IPrimitiveMap, IDictionary>> LazyToDictionary = new Lazy<Func<IPrimitiveMap, IDictionary>>(
            CreatePrimitiveMapToDictonaryFunc, LazyThreadSafetyMode.ExecutionAndPublication);

        public static Func<IPrimitiveMap, IDictionary> ToDictionary
        {
            get { return LazyToDictionary.Value; }
        }

        private static Func<IPrimitiveMap, IDictionary> CreatePrimitiveMapToDictonaryFunc()
        {
            var objectType = typeof(PrimitiveMap);
            var fieldType = typeof(IDictionary);
            const string memberName = "dictionary";
            const string methodName = "Get" + memberName;

            var fieldInfo = objectType.GetField(memberName, BindingFlags.Instance | BindingFlags.NonPublic);
            var dm = new DynamicMethod(methodName, fieldType, new[] { objectType }, objectType);

            var il = dm.GetILGenerator();
            // Load the instance of the object (argument 0) onto the stack
            il.Emit(OpCodes.Ldarg_0);
            // Load the value of the object's field (fi) onto the stack
            il.Emit(OpCodes.Ldfld, fieldInfo);
            // return the value on the top of the stack
            il.Emit(OpCodes.Ret);

            return (Func<IPrimitiveMap, IDictionary>)dm.CreateDelegate(typeof(Func<IPrimitiveMap, IDictionary>));
        }

        private static Func<TObjectType, TFieldType> CreateGetter<TObjectType, TFieldType>(FieldInfo field)
        {
            if (typeof (TFieldType) != field.FieldType)
            {
                throw new InvalidOperationException("FieldTypes must match");
            }
            if (typeof (TObjectType) != field.DeclaringType)
            {
                throw new InvalidOperationException("DeclaringTypes must match");
            }

            var objParm = Expression.Parameter(typeof(TObjectType), "obj");
            var delegateType = typeof(Func<,>).MakeGenericType(typeof(TObjectType), typeof(TFieldType));
            var fieldExpr = Expression.Field(objParm, field.Name);
            var lambda = Expression.Lambda(delegateType, fieldExpr, objParm);
            return (Func<TObjectType, TFieldType>)lambda.Compile();
        }
    }
}