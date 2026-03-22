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
            ReflectionReset.SetStaticField(typeof(Phase1Definitions), "_registered", false);
            ReflectionReset.SetStaticField(typeof(Phase1Bootstrap), "_initialized", false);
            ReflectionReset.SetStaticField(typeof(Phase1StubBootstrap), "_registered", false);
        }

        public static void RecoverEditorState()
        {
            InvokeConventionMethod("Ee4v.Core.I18n.CoreLocalizationDefinitions", "RegisterAll");

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
            ReflectionReset.ClearCollectionField(typeof(SettingApi), "Definitions");
            ReflectionReset.ClearCollectionField(typeof(SettingApi), "CachedValues");
            ReflectionReset.ClearCollectionField(typeof(SettingApi), "LoadedScopes");
            ReflectionReset.ClearCollectionField(typeof(SettingApi), "DirtyScopes");
            ReflectionReset.SetStaticField(typeof(SettingApi), "Changed", null);

            var stores = (System.Collections.IDictionary)ReflectionReset.GetStaticField(typeof(SettingApi), "Stores");
            stores.Clear();
            stores.Add(SettingScope.User, new EditorPrefsSettingStore());
            stores.Add(SettingScope.Project, new ProjectFileSettingStore());

            ReflectionReset.ClearCollectionField(typeof(InjectorApi), "Registrations");
            ReflectionReset.ClearCollectionField(typeof(InjectorApi), "HierarchyHostVersions");
            ReflectionReset.ClearCollectionField(typeof(InjectorApi), "ProjectHostVersions");
            ReflectionReset.SetStaticField(typeof(InjectorApi), "_hierarchyItemRegistrations", Array.Empty<ItemInjectionRegistration>());
            ReflectionReset.SetStaticField(typeof(InjectorApi), "_projectItemRegistrations", Array.Empty<ItemInjectionRegistration>());
            ReflectionReset.SetStaticField(typeof(InjectorApi), "_hierarchyHeaderRegistrations", Array.Empty<VisualElementInjectionRegistration>());
            ReflectionReset.SetStaticField(typeof(InjectorApi), "_projectToolbarRegistrations", Array.Empty<VisualElementInjectionRegistration>());
            ReflectionReset.SetStaticField(typeof(InjectorApi), "_hostsDirty", true);
            ReflectionReset.SetStaticField(typeof(InjectorApi), "_hostVersion", 0);
            ReflectionReset.SetStaticField(typeof(InjectorApi), "_nextHostSyncAt", 0d);

            ReflectionReset.SetStaticField(typeof(PackagePathUtility), "_packageRootAssetPath", null);
            ReflectionReset.SetStaticField(typeof(PackagePathUtility), "_packageRootFullPath", null);
            ReflectionReset.ClearCollectionField(typeof(PackagePathUtility), "SourceFileNamespaceCache");

            ReflectionReset.ClearCollectionField(typeof(I18N), "CallerNamespaceScopeCache");
            ReflectionReset.ClearCollectionField(typeof(I18N), "WarnedCallerSites");
            ReflectionReset.ClearCollectionField(typeof(I18N), "WarnedDuplicateKeys");
            ReflectionReset.SetStaticField(typeof(I18N), "_catalogSnapshot", null);

            ReflectionReset.SetStaticField(typeof(CoreLocalizationDefinitions), "_registered", false);
        }

        private static void InvokeConventionMethod(string fullTypeName, string methodName)
        {
            var type = typeof(SettingApi).Assembly.GetType(fullTypeName);
            if (type == null)
            {
                return;
            }

            var method = type.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null);
            if (method != null)
            {
                method.Invoke(null, null);
            }
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
