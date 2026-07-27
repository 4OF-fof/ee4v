using System;
using System.Collections.Generic;

namespace Ee4v.Core.Localization
{
    public sealed class LocalizationCatalog
    {
        public Dictionary<string, LocalizationLocaleCatalog> Locales { get; } =
            new Dictionary<string, LocalizationLocaleCatalog>(StringComparer.OrdinalIgnoreCase);

        public List<LocalizationDuplicateKey> DuplicateKeys { get; } =
            new List<LocalizationDuplicateKey>();
    }

    public sealed class LocalizationLocaleCatalog
    {
        public LocalizationLocaleCatalog(string locale)
        {
            Locale = locale;
        }

        public string Locale { get; }

        public Dictionary<string, LocalizationScopeCatalog> Scopes { get; } =
            new Dictionary<string, LocalizationScopeCatalog>(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class LocalizationScopeCatalog
    {
        public LocalizationScopeCatalog(string scope)
        {
            Scope = scope;
        }

        public string Scope { get; }

        public Dictionary<string, LocalizationEntry> Entries { get; } =
            new Dictionary<string, LocalizationEntry>(StringComparer.Ordinal);
    }

    public sealed class LocalizationEntry
    {
        public LocalizationEntry(
            string locale,
            string scope,
            string key,
            string value,
            string filePath)
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

    public sealed class LocalizationDuplicateKey
    {
        public LocalizationDuplicateKey(
            string locale,
            string scope,
            string key,
            string originalFilePath,
            string duplicateFilePath)
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
}
