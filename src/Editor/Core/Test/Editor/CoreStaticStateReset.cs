using System;
using System.Collections;
using System.Reflection;
using Ee4v.Core.I18n;
using Ee4v.Core.Background;
using Ee4v.Core.Injector;
using Ee4v.Core.Internal;
using Ee4v.Core.Settings;
using Ee4v.Testing.Application;
using Ee4v.Testing.Contracts;
using Ee4v.Testing.Infrastructure.Unity;
using Ee4v.Testing.UI;
using Ee4v.UI;
using UnityEditor;

namespace Ee4v.Core.Tests
{
    internal static class Ee4vCoreTestReset
    {
        public static void ResetAll()
        {
            FeatureTestRegistryReset.Reset();
            FeatureTestRunnerStateReset.Reset();
            CoreSettings.ResetForTests();
            InjectorApiReset.Reset();
            PackagePathUtilityReset.Reset();
            I18NReset.Reset();
            Ee4vBootstrapFlagReset.Reset();
            WindowToastReset.Reset();
            StatusOverlayReset.Reset();
            CoreBackgroundActivities.ResetForTests();
        }

        public static void RecoverEditorState()
        {
            CoreLocalizationDefinitions.RegisterAll(CoreSettings.Current);
            InvokeAllFeatureBootstraps();
            FeatureTestRegistry.Refresh();
        }

        private static void InvokeAllFeatureBootstraps()
        {
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
                    continue;
                }

                var registerAll = type.GetMethod(
                    "RegisterAll",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    Type.EmptyTypes,
                    null);
                if (registerAll != null)
                {
                    registerAll.Invoke(null, null);
                }
            }
        }

    }

    internal static class FeatureTestRegistryReset
    {
        public static void Reset()
        {
            ReflectionReset.SetStaticField(typeof(FeatureTestRegistry), "_cachedDescriptors", null);
        }
    }

    internal static class FeatureTestRunnerStateReset
    {
        public static void Reset()
        {
            var activeRunner = ReflectionReset.GetStaticField(
                typeof(FeatureTestManagerWindow),
                "_runnerService") as IFeatureTestRunner;
            if (activeRunner != null && activeRunner.IsRunInProgress)
            {
                return;
            }

            FeatureTestManagerWindow.ResetForTests();
            var unityRunner = activeRunner as FeatureTestRunnerService;
            if (unityRunner != null)
            {
                unityRunner.ResetForTests();
                return;
            }

            FeatureTestRunnerService.ClearPersistedState();
        }
    }

    internal static class InjectorApiReset
    {
        public static void Reset()
        {
            InjectorApi.ResetForTests();
        }
    }

    internal static class PackagePathUtilityReset
    {
        public static void Reset()
        {
            ReflectionReset.SetStaticField(typeof(PackagePathUtility), "_packageRootAssetPath", null);
            ReflectionReset.SetStaticField(typeof(PackagePathUtility), "_packageRootFullPath", null);
            ReflectionReset.ClearCollectionField(typeof(PackagePathUtility), "SourceFileNamespaceCache");
        }
    }

    internal static class I18NReset
    {
        public static void Reset()
        {
            ReflectionReset.ClearCollectionField(typeof(I18N), "CallerNamespaceScopeCache");
            ReflectionReset.ClearCollectionField(typeof(I18N), "WarnedCallerSites");
            ReflectionReset.SetStaticField(typeof(I18N), "Reloaded", null);
            CoreLocalization.ResetForTests();
        }
    }

    internal static class Ee4vBootstrapFlagReset
    {
        public static void Reset()
        {
            foreach (var type in typeof(CoreLocalization).Assembly.GetTypes())
            {
                if (type == null || string.IsNullOrWhiteSpace(type.Namespace) || !type.Namespace.StartsWith("Ee4v.", StringComparison.Ordinal))
                {
                    continue;
                }

                ResetFlag(type, "_initialized");
                ResetFlag(type, "_registered");
            }
        }

        private static void ResetFlag(Type type, string fieldName)
        {
            var field = type.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
            if (field == null || field.FieldType != typeof(bool))
            {
                return;
            }

            field.SetValue(null, false);
        }
    }

    internal static class WindowToastReset
    {
        public static void Reset()
        {
            WindowToastApi.ResetAllHosts();
        }
    }

    internal static class StatusOverlayReset
    {
        public static void Reset()
        {
            BackgroundStatusOverlayApi.ResetAllHosts();
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
            if (value is IDictionary dictionary)
            {
                dictionary.Clear();
                return;
            }

            if (value is IList list)
            {
                list.Clear();
                return;
            }

            if (!(value is string) && value is IEnumerable)
            {
                var clearMethod = value.GetType().GetMethod("Clear", Type.EmptyTypes);
                if (clearMethod != null)
                {
                    clearMethod.Invoke(value, null);
                    return;
                }
            }

            throw new InvalidOperationException("Field '" + fieldName + "' is not a clearable collection.");
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
