using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ee4v.Injector;
using Ee4v.Internal;
using Ee4v.Phase1;
using Ee4v.Settings;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditorInternal;

namespace Ee4v.I18n
{
    public static class I18N
    {
        private const string EnglishLocale = "en-US";
        private static readonly Dictionary<string, Dictionary<string, string>> CatalogCache = new Dictionary<string, Dictionary<string, string>>();

        static I18N()
        {
            Phase1Bootstrap.EnsureInitialized();
        }

        public static string CurrentLanguage
        {
            get { return SettingApi.Get(Phase1Definitions.Language); }
        }

        public static string FallbackLanguage
        {
            get { return SettingApi.Get(Phase1Definitions.FallbackLanguage); }
        }

        public static string Get(string key, params object[] args)
        {
            string resolved;
            if (!TryGet(key, out resolved))
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

        public static bool TryGet(string key, out string value)
        {
            foreach (var locale in GetFallbackSequence())
            {
                var catalog = GetOrLoadCatalog(locale);
                if (catalog.TryGetValue(key, out value))
                {
                    return true;
                }
            }

            value = null;
            return false;
        }

        public static IReadOnlyList<string> GetAvailableLanguages()
        {
            return PackagePathUtility.GetLocalizationRootFullPaths()
                .Where(Directory.Exists)
                .SelectMany(Directory.GetDirectories)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrEmpty(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public static void Reload()
        {
            CatalogCache.Clear();
            InjectorApi.Repaint(InjectionChannel.HierarchyHeader);
            InjectorApi.Repaint(InjectionChannel.ProjectToolbar);
            InternalEditorUtility.RepaintAllViews();
            EditorApplication.RepaintHierarchyWindow();
            EditorApplication.RepaintProjectWindow();
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

        private static Dictionary<string, string> GetOrLoadCatalog(string locale)
        {
            Dictionary<string, string> catalog;
            if (CatalogCache.TryGetValue(locale, out catalog))
            {
                return catalog;
            }

            catalog = LoadCatalog(locale);
            CatalogCache[locale] = catalog;
            return catalog;
        }

        private static Dictionary<string, string> LoadCatalog(string locale)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            var localeDirectories = PackagePathUtility.GetLocalizationRootFullPaths()
                .Select(rootPath => Path.Combine(rootPath, locale))
                .Where(Directory.Exists)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            for (var directoryIndex = 0; directoryIndex < localeDirectories.Length; directoryIndex++)
            {
                var files = Directory.GetFiles(localeDirectories[directoryIndex], "*.jsonc", SearchOption.TopDirectoryOnly)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

                foreach (var filePath in files)
                {
                    try
                    {
                        var content = File.ReadAllText(filePath);
                        var normalized = JsoncUtility.Normalize(content);
                        var root = JObject.Parse(normalized);
                        Flatten(root, string.Empty, result);
                    }
                    catch (Exception exception)
                    {
                        UnityEngine.Debug.LogError("[ee4v:i18n] Failed to parse " + filePath + "\n" + exception.Message);
                    }
                }
            }

            return result;
        }

        private static void Flatten(JToken token, string prefix, IDictionary<string, string> output)
        {
            if (token == null)
            {
                return;
            }

            if (token.Type == JTokenType.Object)
            {
                foreach (var property in token.Children<JProperty>())
                {
                    var nextPrefix = string.IsNullOrEmpty(prefix) ? property.Name : prefix + "." + property.Name;
                    Flatten(property.Value, nextPrefix, output);
                }

                return;
            }

            if (token.Type == JTokenType.Array)
            {
                output[prefix] = string.Join(", ", token.Values<string>().ToArray());
                return;
            }

            output[prefix] = token.Value<string>() ?? string.Empty;
        }
    }
}
