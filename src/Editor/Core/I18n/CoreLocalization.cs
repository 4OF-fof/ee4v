using System;
using System.Collections.Generic;
using Ee4v.Core.Localization;
using Ee4v.Core.Settings;
using UnityEngine;

namespace Ee4v.Core.I18n
{
    public static class CoreLocalization
    {
        private static ILocalizationService _current;

        static CoreLocalization()
        {
            Replace(CreateDefault());
        }

        public static event EventHandler Reloaded;

        public static ILocalizationService Current
        {
            get { return _current; }
        }

        internal static void ResetForTests(ILocalizationService replacement = null)
        {
            Replace(replacement ?? CreateDefault());
        }

        private static void Replace(ILocalizationService replacement)
        {
            if (_current != null)
            {
                _current.Reloaded -= OnReloaded;
            }

            _current = replacement;
            _current.Reloaded += OnReloaded;
        }

        private static void OnReloaded(object sender, EventArgs args)
        {
            Reloaded?.Invoke(sender, args);
        }

        private static ILocalizationService CreateDefault()
        {
            return new LocalizationService(
                new PackageLocalizationCatalogSource(),
                new SettingsLocalizationLanguageProvider(CoreSettings.Current),
                new UnityLocalizationDiagnostics());
        }
    }

    internal sealed class PackageLocalizationCatalogSource : ILocalizationCatalogSource
    {
        public LocalizationCatalog Load()
        {
            return LocalizationCatalogLoader.Load();
        }
    }

    internal sealed class SettingsLocalizationLanguageProvider : ILocalizationLanguageProvider
    {
        private readonly ISettingsService _settings;

        public SettingsLocalizationLanguageProvider(ISettingsService settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public string CurrentLanguage
        {
            get
            {
                CoreLocalizationDefinitions.RegisterAll(_settings);
                return _settings.Get(CoreLocalizationDefinitions.Language);
            }
        }

        public string FallbackLanguage
        {
            get
            {
                CoreLocalizationDefinitions.RegisterAll(_settings);
                return _settings.Get(CoreLocalizationDefinitions.FallbackLanguage);
            }
        }
    }

    internal sealed class UnityLocalizationDiagnostics : ILocalizationDiagnostics
    {
        private readonly HashSet<string> _reported =
            new HashSet<string>(StringComparer.Ordinal);

        public void ReportDuplicates(IReadOnlyList<LocalizationDuplicateKey> duplicates)
        {
            foreach (var duplicate in duplicates)
            {
                var id = duplicate.Locale + "|" + duplicate.Scope + "|" +
                         duplicate.Key + "|" + duplicate.DuplicateFilePath;
                if (!_reported.Add(id))
                {
                    continue;
                }

                Debug.LogError(
                    "[ee4v:i18n] Duplicate key '" + duplicate.Key +
                    "' in scope '" + duplicate.Scope +
                    "', locale '" + duplicate.Locale +
                    "'. Original: " + duplicate.OriginalFilePath +
                    " Duplicate: " + duplicate.DuplicateFilePath);
            }
        }
    }
}
