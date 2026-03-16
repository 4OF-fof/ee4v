using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Ee4v.Core.Injector;
using Ee4v.Core.Internal;
using Ee4v.Core.Settings;
using UnityEditor;
using UnityEditorInternal;

namespace Ee4v.Core.I18n
{
    public static class I18N
    {
        private const string EnglishLocale = "en-US";
        private static readonly Dictionary<string, string> CallerNamespaceScopeCache = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly HashSet<string> WarnedCallerSites = new HashSet<string>(StringComparer.Ordinal);
        private static readonly HashSet<string> WarnedDuplicateKeys = new HashSet<string>(StringComparer.Ordinal);
        private static LocalizationCatalogSnapshot _catalogSnapshot;

        public static string CurrentLanguage
        {
            get { return SettingApi.Get(CoreLocalizationDefinitions.Language); }
        }

        public static string FallbackLanguage
        {
            get { return SettingApi.Get(CoreLocalizationDefinitions.FallbackLanguage); }
        }

        public static string Get(string key, params object[] args)
        {
            return GetForScope(ResolveCallerScope(), key, args);
        }

        public static bool TryGet(string key, out string value)
        {
            return TryGetForScope(ResolveCallerScope(), key, out value);
        }

        internal static string GetForScope(string scope, string key, params object[] args)
        {
            string resolved;
            if (!TryGetForScope(scope, key, out resolved))
            {
                resolved = key;
            }

            if (args == null || args.Length == 0)
            {
                return resolved;
            }

            try
            {
                return string.Format(resolved, args);
            }
            catch (FormatException)
            {
                return key;
            }
        }

        internal static bool TryGetForScope(string scope, string key, out string value)
        {
            if (string.IsNullOrWhiteSpace(scope))
            {
                value = null;
                return false;
            }

            foreach (var locale in GetFallbackSequence())
            {
                var entry = GetEntry(scope, locale, key);
                if (entry != null)
                {
                    value = entry.Value;
                    return true;
                }
            }

            value = null;
            return false;
        }

        public static IReadOnlyList<string> GetAvailableLanguages()
        {
            return EnsureCatalogLoaded().Locales.Keys
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public static void Reload()
        {
            _catalogSnapshot = null;
            CallerNamespaceScopeCache.Clear();
            WarnedDuplicateKeys.Clear();
            InjectorApi.Repaint(InjectionChannel.HierarchyHeader);
            InjectorApi.Repaint(InjectionChannel.ProjectToolbar);
            InternalEditorUtility.RepaintAllViews();
        }

        private static IEnumerable<string> GetFallbackSequence()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var sequence = new[] { CurrentLanguage, FallbackLanguage, EnglishLocale };

            for (var i = 0; i < sequence.Length; i++)
            {
                var locale = sequence[i];
                if (string.IsNullOrWhiteSpace(locale) || !seen.Add(locale))
                {
                    continue;
                }

                yield return locale;
            }
        }

        internal static string ResolveScopeForNamespace(string namespaceName)
        {
            if (string.IsNullOrWhiteSpace(namespaceName))
            {
                return null;
            }

            string scope;
            if (CallerNamespaceScopeCache.TryGetValue(namespaceName, out scope))
            {
                return scope;
            }

            scope = PackagePathUtility.GetScopeNameForNamespace(namespaceName);
            CallerNamespaceScopeCache[namespaceName] = scope;
            return scope;
        }

        private static LocalizationEntry GetEntry(string scope, string locale, string key)
        {
            LocalizationLocaleCatalog localeCatalog;
            if (!EnsureCatalogLoaded().Locales.TryGetValue(locale, out localeCatalog))
            {
                return null;
            }

            LocalizationScopeCatalog scopeCatalog;
            if (!localeCatalog.Scopes.TryGetValue(scope, out scopeCatalog))
            {
                return null;
            }

            LocalizationEntry entry;
            return scopeCatalog.Entries.TryGetValue(key, out entry) ? entry : null;
        }

        private static LocalizationCatalogSnapshot EnsureCatalogLoaded()
        {
            if (_catalogSnapshot != null)
            {
                return _catalogSnapshot;
            }

            _catalogSnapshot = LocalizationCatalogLoader.Load();
            ReportDuplicateKeys(_catalogSnapshot.DuplicateKeys);
            return _catalogSnapshot;
        }

        private static void ReportDuplicateKeys(IReadOnlyList<LocalizationDuplicateKey> duplicates)
        {
            for (var i = 0; i < duplicates.Count; i++)
            {
                var duplicate = duplicates[i];
                var duplicateId = duplicate.Locale + "|" + duplicate.Scope + "|" + duplicate.Key + "|" + duplicate.DuplicateFilePath;
                if (WarnedDuplicateKeys.Add(duplicateId))
                {
                    UnityEngine.Debug.LogError(
                        "[ee4v:i18n] Duplicate key '" + duplicate.Key + "' in scope '" + duplicate.Scope +
                        "', locale '" + duplicate.Locale + "'. Original: " + duplicate.OriginalFilePath +
                        " Duplicate: " + duplicate.DuplicateFilePath);
                }
            }
        }

        private static string ResolveCallerScope()
        {
            var stackTrace = new StackTrace();
            for (var i = 1; i < stackTrace.FrameCount; i++)
            {
                var frame = stackTrace.GetFrame(i);
                if (frame == null)
                {
                    continue;
                }

                var method = frame.GetMethod();
                var declaringType = method != null ? method.DeclaringType : null;
                if (declaringType == null || declaringType == typeof(I18N))
                {
                    continue;
                }

                var scope = ResolveScopeForNamespace(declaringType.Namespace);
                if (!string.IsNullOrWhiteSpace(scope))
                {
                    return scope;
                }

                var callerSite = declaringType.FullName ?? method.Name;
                if (WarnedCallerSites.Add(callerSite))
                {
                    UnityEngine.Debug.LogWarning("[ee4v:i18n] Failed to resolve scope from namespace for caller: " + callerSite);
                }
            }

            return null;
        }
    }
}
