using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ee4v.Core.Internal;
using Newtonsoft.Json.Linq;

namespace Ee4v.Core.I18n
{
    internal sealed class LocalizationCatalogSnapshot
    {
        public Dictionary<string, LocalizationLocaleCatalog> Locales { get; } =
            new Dictionary<string, LocalizationLocaleCatalog>(StringComparer.OrdinalIgnoreCase);

        public List<LocalizationDuplicateKey> DuplicateKeys { get; } = new List<LocalizationDuplicateKey>();
    }

    internal sealed class LocalizationLocaleCatalog
    {
        public LocalizationLocaleCatalog(string locale)
        {
            Locale = locale;
        }

        public string Locale { get; }

        public Dictionary<string, LocalizationScopeCatalog> Scopes { get; } =
            new Dictionary<string, LocalizationScopeCatalog>(StringComparer.OrdinalIgnoreCase);
    }

    internal sealed class LocalizationScopeCatalog
    {
        public LocalizationScopeCatalog(string scope)
        {
            Scope = scope;
        }

        public string Scope { get; }

        public Dictionary<string, LocalizationEntry> Entries { get; } =
            new Dictionary<string, LocalizationEntry>(StringComparer.Ordinal);
    }

    internal sealed class LocalizationEntry
    {
        public LocalizationEntry(string locale, string scope, string key, string value, string filePath)
        {
            Locale = locale;
            Scope = scope;
            Key = key;
            Value = value;
            FilePath = filePath;
        }

        public string Locale { get; }

        public string Scope { get; }

        public string Key { get; }

        public string Value { get; }

        public string FilePath { get; }
    }

    internal sealed class LocalizationDuplicateKey
    {
        public LocalizationDuplicateKey(string locale, string scope, string key, string originalFilePath, string duplicateFilePath)
        {
            Locale = locale;
            Scope = scope;
            Key = key;
            OriginalFilePath = originalFilePath;
            DuplicateFilePath = duplicateFilePath;
        }

        public string Locale { get; }

        public string Scope { get; }

        public string Key { get; }

        public string OriginalFilePath { get; }

        public string DuplicateFilePath { get; }
    }

    internal static class LocalizationCatalogLoader
    {
        public static LocalizationCatalogSnapshot Load()
        {
            var snapshot = new LocalizationCatalogSnapshot();
            var roots = PackagePathUtility.GetLocalizationRootFullPaths().ToArray();

            for (var i = 0; i < roots.Length; i++)
            {
                var rootPath = roots[i];
                if (!Directory.Exists(rootPath))
                {
                    continue;
                }

                var scope = PackagePathUtility.GetScopeNameForLocalizationRoot(rootPath);
                if (string.IsNullOrWhiteSpace(scope))
                {
                    continue;
                }

                var localeDirectories = Directory.GetDirectories(rootPath)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                for (var localeIndex = 0; localeIndex < localeDirectories.Length; localeIndex++)
                {
                    var localeDirectory = localeDirectories[localeIndex];
                    var locale = Path.GetFileName(localeDirectory);
                    if (string.IsNullOrWhiteSpace(locale))
                    {
                        continue;
                    }

                    LocalizationLocaleCatalog localeCatalog;
                    if (!snapshot.Locales.TryGetValue(locale, out localeCatalog))
                    {
                        localeCatalog = new LocalizationLocaleCatalog(locale);
                        snapshot.Locales.Add(locale, localeCatalog);
                    }

                    LocalizationScopeCatalog scopeCatalog;
                    if (!localeCatalog.Scopes.TryGetValue(scope, out scopeCatalog))
                    {
                        scopeCatalog = new LocalizationScopeCatalog(scope);
                        localeCatalog.Scopes.Add(scope, scopeCatalog);
                    }

                    var files = Directory.GetFiles(localeDirectory, "*.jsonc", SearchOption.TopDirectoryOnly)
                        .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

                    foreach (var filePath in files)
                    {
                        Dictionary<string, string> flattened;
                        try
                        {
                            flattened = ParseFile(filePath);
                        }
                        catch (Exception exception)
                        {
                            UnityEngine.Debug.LogError("[ee4v:i18n] Failed to parse " + filePath + "\n" + exception.Message);
                            continue;
                        }

                        foreach (var pair in flattened)
                        {
                            LocalizationEntry existing;
                            if (scopeCatalog.Entries.TryGetValue(pair.Key, out existing))
                            {
                                snapshot.DuplicateKeys.Add(new LocalizationDuplicateKey(
                                    locale,
                                    scope,
                                    pair.Key,
                                    existing.FilePath,
                                    filePath));
                                continue;
                            }

                            scopeCatalog.Entries.Add(
                                pair.Key,
                                new LocalizationEntry(locale, scope, pair.Key, pair.Value, filePath));
                        }
                    }
                }
            }

            return snapshot;
        }

        private static Dictionary<string, string> ParseFile(string filePath)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);

            var content = File.ReadAllText(filePath);
            var normalized = JsoncUtility.Normalize(content);
            var root = JObject.Parse(normalized);
            Flatten(root, string.Empty, result);

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
