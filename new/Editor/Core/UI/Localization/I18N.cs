using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Ee4v.Core.I18n;
using Ee4v.Core.Injector;
using Ee4v.Core.Internal;
using UnityEditorInternal;

namespace Ee4v.Core.I18n
{
    public static class I18N
    {
        private static readonly Dictionary<string, string> CallerNamespaceScopeCache =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly HashSet<string> WarnedCallerSites =
            new HashSet<string>(StringComparer.Ordinal);

        static I18N()
        {
            CoreLocalization.Reloaded += OnReloaded;
        }

        internal static event Action Reloaded;

        public static string CurrentLanguage
        {
            get
            {
                CoreLocalizationDefinitions.RegisterAll(
                    Ee4v.Core.Settings.CoreSettings.Current);
                return CoreLocalizationDefinitions.Language != null
                    ? Ee4v.Core.Settings.CoreSettings.Current.Get(
                        CoreLocalizationDefinitions.Language)
                    : string.Empty;
            }
        }

        public static string FallbackLanguage
        {
            get
            {
                CoreLocalizationDefinitions.RegisterAll(
                    Ee4v.Core.Settings.CoreSettings.Current);
                return CoreLocalizationDefinitions.FallbackLanguage != null
                    ? Ee4v.Core.Settings.CoreSettings.Current.Get(
                        CoreLocalizationDefinitions.FallbackLanguage)
                    : string.Empty;
            }
        }

        public static string Get(
            string key,
            [CallerFilePath] string callerFilePath = null)
        {
            return GetForScope(ResolveCallerScope(callerFilePath), key);
        }

        public static string Get(string key, params object[] arguments)
        {
            return GetForScope(ResolveCallerScopeFromStack(2), key, arguments);
        }

        public static bool TryGet(
            string key,
            out string value,
            [CallerFilePath] string callerFilePath = null)
        {
            return TryGetForScope(ResolveCallerScope(callerFilePath), key, out value);
        }

        internal static string GetForScope(
            string scope,
            string key,
            params object[] arguments)
        {
            return string.IsNullOrWhiteSpace(scope)
                ? key
                : CoreLocalization.Current.ForScope(scope).Get(key, arguments);
        }

        internal static bool TryGetForScope(
            string scope,
            string key,
            out string value)
        {
            if (string.IsNullOrWhiteSpace(scope))
            {
                value = null;
                return false;
            }

            return CoreLocalization.Current.ForScope(scope).TryGet(key, out value);
        }

        public static IReadOnlyList<string> GetAvailableLanguages()
        {
            return CoreLocalization.Current.GetAvailableLanguages();
        }

        public static void Reload()
        {
            CoreLocalization.Current.Reload();
        }

        internal static string ResolveScopeForNamespace(string namespaceName)
        {
            if (string.IsNullOrWhiteSpace(namespaceName))
            {
                return null;
            }

            if (!CallerNamespaceScopeCache.TryGetValue(namespaceName, out var scope))
            {
                scope = PackagePathUtility.GetScopeNameForNamespace(namespaceName);
                CallerNamespaceScopeCache[namespaceName] = scope;
            }

            return scope;
        }

        private static string ResolveCallerScope(string callerFilePath)
        {
            var scope = string.IsNullOrWhiteSpace(callerFilePath)
                ? null
                : ResolveScopeForNamespace(
                    PackagePathUtility.GetDeclaredNamespace(callerFilePath));
            return !string.IsNullOrWhiteSpace(scope)
                ? scope
                : ResolveCallerScopeFromStack(3);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string ResolveCallerScopeFromStack(int skipFrames)
        {
            var frame = new StackFrame(skipFrames, false);
            var method = frame.GetMethod();
            var declaringType = method != null ? method.DeclaringType : null;
            if (declaringType == null || declaringType == typeof(I18N))
            {
                return null;
            }

            var scope = ResolveScopeForNamespace(declaringType.Namespace);
            if (!string.IsNullOrWhiteSpace(scope))
            {
                return scope;
            }

            var callerSite = declaringType.FullName ?? method.Name;
            if (WarnedCallerSites.Add(callerSite))
            {
                UnityEngine.Debug.LogWarning(
                    "[ee4v:i18n] Failed to resolve scope from namespace for caller: " +
                    callerSite);
            }

            return null;
        }

        private static void OnReloaded(object sender, EventArgs args)
        {
            CallerNamespaceScopeCache.Clear();
            InjectorApi.Repaint(InjectionChannel.ProjectToolbar);
            InternalEditorUtility.RepaintAllViews();
            Reloaded?.Invoke();
        }
    }
}
