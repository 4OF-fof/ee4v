using System;
using System.Linq.Expressions;
using System.Reflection;

namespace _4OF.ee4v.Core.Wraps {
    internal abstract class WrapBase {
        protected static (Func<object, T> g, Action<object, T> s) GetField<T>(Type type, string name) {
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null) return (null, null);

            var targetParam = Expression.Parameter(typeof(object), "target");
            var valueParam = Expression.Parameter(typeof(T), "value");
            var castTarget = Expression.Convert(targetParam, type);
            var fieldExp = Expression.Field(castTarget, field);

            var getterExp = Expression.Convert(fieldExp, typeof(T));
            var getter = Expression.Lambda<Func<object, T>>(getterExp, targetParam).Compile();

            var setterValueExp = Expression.Convert(valueParam, field.FieldType);
            var assign = Expression.Assign(fieldExp, setterValueExp);
            var setter = Expression.Lambda<Action<object, T>>(assign, targetParam, valueParam).Compile();

            return (getter, setter);
        }

        protected static (Func<object, T> g, Action<object, T> s) GetProperty<T>(Type type, string name) {
            var prop = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop == null) return (null, null);

            var targetParam = Expression.Parameter(typeof(object), "target");
            var valueParam = Expression.Parameter(typeof(T), "value");
            var castTarget = Expression.Convert(targetParam, type);

            Func<object, T> getter = null;
            if (prop.CanRead) {
                var propExp = Expression.Property(castTarget, prop);
                var getterExp = Expression.Convert(propExp, typeof(T));
                getter = Expression.Lambda<Func<object, T>>(getterExp, targetParam).Compile();
            }

            Action<object, T> setter = null;
            if (prop.CanWrite) {
                var propExp = Expression.Property(castTarget, prop);
                var setterValueExp = Expression.Convert(valueParam, prop.PropertyType);
                var assign = Expression.Assign(propExp, setterValueExp);
                setter = Expression.Lambda<Action<object, T>>(assign, targetParam, valueParam).Compile();
            }

            return (getter, setter);
        }

        protected static Action<object> GetAction(Type type, string name) {
            var method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method == null) return null;

            var targetParam = Expression.Parameter(typeof(object), "target");
            var castTarget = Expression.Convert(targetParam, type);
            var call = Expression.Call(castTarget, method);
            return Expression.Lambda<Action<object>>(call, targetParam).Compile();
        }

        protected static Action<object, T> GetAction<T>(Type type, string name) {
            var method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(T) }, null);
            if (method == null) return null;

            var targetParam = Expression.Parameter(typeof(object), "target");
            var argParam = Expression.Parameter(typeof(T), "arg");
            
            var castTarget = Expression.Convert(targetParam, type);
            var call = Expression.Call(castTarget, method, argParam);
            return Expression.Lambda<Action<object, T>>(call, targetParam, argParam).Compile();
        }

        protected static (Func<T> g, Action<T> s) GetStaticProperty<T>(Type type, string name) {
            var prop = type.GetProperty(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop == null) return (null, null);

            var valueParam = Expression.Parameter(typeof(T), "value");

            Func<T> getter = null;
            if (prop.CanRead) {
                var propExp = Expression.Property(null, prop);
                var getterExp = Expression.Convert(propExp, typeof(T));
                getter = Expression.Lambda<Func<T>>(getterExp).Compile();
            }

            Action<T> setter = null;
            if (prop.CanWrite) {
                var propExp = Expression.Property(null, prop);
                var setterValueExp = Expression.Convert(valueParam, prop.PropertyType);
                var assign = Expression.Assign(propExp, setterValueExp);
                setter = Expression.Lambda<Action<T>>(assign, valueParam).Compile();
            }

            return (getter, setter);
        }
        
        protected static Func<object, object[], object> GetMethod(Type type, string name, Type[] types) {
            var method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, types, null);
            if (method == null) return null;
            return (target, args) => method.Invoke(target, args);
        }
    }
}