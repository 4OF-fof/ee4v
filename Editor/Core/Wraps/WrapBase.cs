using System;
using System.Linq.Expressions;
using System.Reflection;

namespace _4OF.ee4v.Core.Wraps {
    internal abstract class WrapBase {
        protected static (Func<object, object> g, Action<object, object> s) GetField(Type type, string name) {
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null) return (null, null);

            return (
                CreateInstanceGetter(field),
                CreateInstanceSetter(field)
            );
        }

        protected static (Func<object, object> g, Action<object, object> s) GetProperty(Type type, string name) {
            var prop = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop == null) return (null, null);

            return (
                CreateInstanceGetter(prop),
                CreateInstanceSetter(prop)
            );
        }

        protected static (Func<object> g, Action<object> s) GetStaticField(Type type, string name) {
            var field = type.GetField(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null) return (null, null);

            return (
                CreateStaticGetter(field),
                CreateStaticSetter(field)
            );
        }

        protected static (Func<object> g, Action<object> s) GetStaticProperty(Type type, string name) {
            var prop = type.GetProperty(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop == null) return (null, null);

            return (
                CreateStaticGetter(prop),
                CreateStaticSetter(prop)
            );
        }

        protected static Func<object, object[], object> GetMethod(Type type, string name, Type[] types = null) {
            var method = types == null
                ? type.GetMethod(name,
                    BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                : type.GetMethod(name,
                    BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null,
                    types, null);

            if (method == null) return null;

            return CreateMethodInvoker(method);
        }

        private static Func<object, object> CreateInstanceGetter(MemberInfo member) {
            var targetParam = Expression.Parameter(typeof(object), "target");

            Expression body = member switch {
                FieldInfo field => Expression.Field(
                    Expression.Convert(targetParam, field.DeclaringType ?? throw new InvalidOperationException()),
                    field),
                PropertyInfo prop => Expression.Property(
                    Expression.Convert(targetParam, prop.DeclaringType ?? throw new InvalidOperationException()), prop),
                _ => null
            };

            if (body == null) return null;

            return Expression.Lambda<Func<object, object>>(
                Expression.Convert(body, typeof(object)), targetParam).Compile();
        }

        private static Action<object, object> CreateInstanceSetter(MemberInfo member) {
            var targetParam = Expression.Parameter(typeof(object), "target");
            var valueParam = Expression.Parameter(typeof(object), "value");
            Expression left = null;
            Type memberType = null;

            switch (member) {
                case FieldInfo field:
                    left = Expression.Field(
                        Expression.Convert(targetParam, field.DeclaringType ?? throw new InvalidOperationException()),
                        field);
                    memberType = field.FieldType;
                    break;
                case PropertyInfo { CanWrite: true } prop:
                    left = Expression.Property(
                        Expression.Convert(targetParam, prop.DeclaringType ?? throw new InvalidOperationException()),
                        prop);
                    memberType = prop.PropertyType;
                    break;
            }

            if (left == null) return (_, _) => { };

            var body = Expression.Assign(left, Expression.Convert(valueParam, memberType));
            return Expression.Lambda<Action<object, object>>(body, targetParam, valueParam).Compile();
        }

        private static Func<object> CreateStaticGetter(MemberInfo member) {
            Expression body = member switch {
                FieldInfo field   => Expression.Field(null, field),
                PropertyInfo prop => Expression.Property(null, prop),
                _                 => null
            };

            if (body == null) return null;

            return Expression.Lambda<Func<object>>(
                Expression.Convert(body, typeof(object))).Compile();
        }

        private static Action<object> CreateStaticSetter(MemberInfo member) {
            var valueParam = Expression.Parameter(typeof(object), "value");
            Expression left = null;
            Type memberType = null;

            switch (member) {
                case FieldInfo field:
                    left = Expression.Field(null, field);
                    memberType = field.FieldType;
                    break;
                case PropertyInfo { CanWrite: true } prop:
                    left = Expression.Property(null, prop);
                    memberType = prop.PropertyType;
                    break;
            }

            if (left == null) return _ => { };

            var body = Expression.Assign(left, Expression.Convert(valueParam, memberType));
            return Expression.Lambda<Action<object>>(body, valueParam).Compile();
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
                var instance = Expression.Convert(targetParam,
                    method.DeclaringType ?? throw new InvalidOperationException());
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