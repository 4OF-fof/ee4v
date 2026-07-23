using System;
using System.Reflection;
using Ee4v.Core.I18n;
using Ee4v.Core.Injector;
using Ee4v.Core.Internal;
using Ee4v.Core.Settings;
using UnityEditor;

namespace Ee4v.Phase1.Tests
{
    internal static class Ee4vPhase1TestReset
    {
        public static void ResetAll()
        {
            ResetCore();
            ReflectionReset.SetStaticField(typeof(Phase1Bootstrap), "_initialized", false);
            ReflectionReset.SetStaticField(typeof(Phase1StubBootstrap), "_registered", false);
            ReflectionReset.SetStaticField(typeof(Phase1StubBootstrap), "_settings", null);
        }

        public static void RecoverEditorState()
        {
            CoreLocalizationDefinitions.RegisterAll(CoreSettings.Current);

            foreach (var type in TypeCache.GetTypesWithAttribute<InitializeOnLoadAttribute>())
            {
                if (type == null || string.IsNullOrWhiteSpace(type.Namespace) || !type.Namespace.StartsWith("Ee4v.", StringComparison.Ordinal))
                {
                    continue;
                }

                var ensureInitialized = type.GetMethod(
                    "EnsureInitialized",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    Type.EmptyTypes,
                    null);
                if (ensureInitialized != null)
                {
                    ensureInitialized.Invoke(null, null);
                }
            }
        }

        private static void ResetCore()
        {
            CoreSettings.ResetForTests();

            InjectorApi.ResetForTests();

            ReflectionReset.SetStaticField(typeof(PackagePathUtility), "_packageRootAssetPath", null);
            ReflectionReset.SetStaticField(typeof(PackagePathUtility), "_packageRootFullPath", null);
            ReflectionReset.ClearCollectionField(typeof(PackagePathUtility), "SourceFileNamespaceCache");

            ReflectionReset.ClearCollectionField(typeof(I18N), "CallerNamespaceScopeCache");
            ReflectionReset.ClearCollectionField(typeof(I18N), "WarnedCallerSites");
            ReflectionReset.SetStaticField(typeof(I18N), "Reloaded", null);
            CoreLocalization.ResetForTests();

        }
    }

    internal static class ReflectionReset
    {
        private const BindingFlags Flags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

        public static object GetStaticField(Type type, string fieldName)
        {
            return GetField(type, fieldName).GetValue(null);
        }

        public static void SetStaticField(Type type, string fieldName, object value)
        {
            GetField(type, fieldName).SetValue(null, value);
        }

        public static void ClearCollectionField(Type type, string fieldName)
        {
            var value = GetStaticField(type, fieldName);
            var clearMethod = value.GetType().GetMethod("Clear", Type.EmptyTypes);
            if (clearMethod == null)
            {
                throw new InvalidOperationException("Field '" + fieldName + "' is not a clearable collection.");
            }

            clearMethod.Invoke(value, null);
        }

        private static FieldInfo GetField(Type type, string fieldName)
        {
            var field = type.GetField(fieldName, Flags);
            if (field == null)
            {
                throw new InvalidOperationException(
                    "Field '" + fieldName + "' was not found on '" + type.FullName + "'.");
            }

            return field;
        }
    }
}
