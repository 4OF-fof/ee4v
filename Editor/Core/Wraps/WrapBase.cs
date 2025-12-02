using System;
using System.Linq.Expressions;
using System.Reflection;

namespace _4OF.ee4v.Core.Wraps {
    internal abstract class WrapBase {

        protected static (Func<object> g, Action<object> s) GetField(Type type, string name) {
            var field = type.GetField(name,
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null) return (null, null);

            return (
                CreateGetter(field),
                CreateSetter(field)
            );
        }

        protected static (Func<T> g, Action<T> s) GetStaticField<T>(Type type, string name) {
            var field = type.GetField(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null) return (null, null);

            return (
                CreateStaticGetter<T>(field),
                CreateStaticSetter<T>(field)
            );
        }

        protected static (Func<object> g, Action<object> s) GetProperty(Type type, string name) {
            var prop = type.GetProperty(name,
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop == null) return (null, null);

            return (
                CreateGetter(prop),
                CreateSetter(prop)
            );
        }

        protected static Func<object, object[], object> GetMethod(Type type, string name, Type[] types = null) {
            var method = types == null
                ? type.GetMethod(name,
                    BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                : type.GetMethod(name,
                    BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null,
                    types, null);

            return method == null ? null : CreateMethodInvoker(method);
        }

        private static Func<object> CreateGetter(MemberInfo member) {
            if (member is FieldInfo field && field.IsStatic) {
                var body = Expression.Convert(Expression.Field(null, field), typeof(object));
                return Expression.Lambda<Func<object>>(body).Compile();
            }

            if (member is not PropertyInfo prop || !prop.GetMethod.IsStatic)
                return () => throw new NotImplementedException("Instance getters need target object.");
            {
                var body = Expression.Convert(Expression.Property(null, prop), typeof(object));
                return Expression.Lambda<Func<object>>(body).Compile();
            }
        }

        private static Func<T> CreateStaticGetter<T>(FieldInfo field) {
            var body = Expression.Field(null, field);
            return Expression.Lambda<Func<T>>(body).Compile();
        }

        private static Action<T> CreateStaticSetter<T>(FieldInfo field) {
            var valueParam = Expression.Parameter(typeof(T), "value");
            var body = Expression.Assign(Expression.Field(null, field), valueParam);
            return Expression.Lambda<Action<T>>(body, valueParam).Compile();
        }

        private static Action<object> CreateSetter(MemberInfo member) {
            return obj => throw new NotImplementedException("Instance setters need target object.");
        }

        private static Func<object, object[], object> CreateMethodInvoker(MethodInfo method) {
            var targetParam = Expression.Parameter(typeof(object), "target");
            var argsParam = Expression.Parameter(typeof(object[]), "args");

            var paramsInfo = method.GetParameters();
            var callArgs = new Expression[paramsInfo.Length];

            for (var i = 0; i < paramsInfo.Length; i++) {
                var index = Expression.Constant(i);
                var paramType = paramsInfo[i].ParameterType;
                var paramAccessor = Expression.ArrayIndex(argsParam, index);
                callArgs[i] = Expression.Convert(paramAccessor, paramType);
            }

            Expression call;
            if (method.IsStatic) {
                call = Expression.Call(method, callArgs);
            }
            else {
                var instance = Expression.Convert(targetParam, method.DeclaringType);
                call = Expression.Call(instance, method, callArgs);
            }

            if (method.ReturnType == typeof(void)) {
                var lambda = Expression.Lambda<Action<object, object[]>>(call, targetParam, argsParam).Compile();
                return (t, a) =>
                {
                    lambda(t, a);
                    return null;
                };
            }

            var result = Expression.Convert(call, typeof(object));
            return Expression.Lambda<Func<object, object[], object>>(result, targetParam, argsParam).Compile();
        }
    }
}