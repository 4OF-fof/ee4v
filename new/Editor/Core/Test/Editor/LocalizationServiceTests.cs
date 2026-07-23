using System;
using System.Collections.Generic;
using Ee4v.Core.Localization;
using NUnit.Framework;

namespace Ee4v.Core.Tests
{
    public sealed class LocalizationServiceTests
    {
        [Test]
        public void ScopedLocalizer_UsesCurrentFallbackAndEnglishInOrder()
        {
            var catalog = CreateCatalog(
                ("ja-JP", "Core", "key.current", "current"),
                ("en-US", "Core", "key.english", "english"));
            var service = new LocalizationService(
                new StubSource(catalog),
                new StubLanguages("ja-JP", "fr-FR"),
                new StubDiagnostics());
            var localizer = service.ForScope("Core");

            Assert.That(localizer.Get("key.current"), Is.EqualTo("current"));
            Assert.That(localizer.Get("key.english"), Is.EqualTo("english"));
            Assert.That(localizer.Get("missing"), Is.EqualTo("missing"));
        }

        [Test]
        public void Reload_DropsCatalogCacheAndRaisesEvent()
        {
            var source = new StubSource(CreateCatalog(
                ("en-US", "Core", "testing.window.title", "first")));
            var service = new LocalizationService(
                source,
                new StubLanguages("en-US", "en-US"),
                new StubDiagnostics());
            var reloadCount = 0;
            service.Reloaded += (_, __) => reloadCount++;

            Assert.That(
                service.ForScope("Core").Get("testing.window.title"),
                Is.EqualTo("first"));
            source.Catalog = CreateCatalog(
                ("en-US", "Core", "testing.window.title", "second"));
            service.Reload();

            Assert.That(
                service.ForScope("Core").Get("testing.window.title"),
                Is.EqualTo("second"));
            Assert.That(source.LoadCount, Is.EqualTo(2));
            Assert.That(reloadCount, Is.EqualTo(1));
        }

        [Test]
        public void CatalogDiagnostics_AreReportedWhenCatalogLoads()
        {
            var catalog = CreateCatalog(("en-US", "Core", "key", "value"));
            catalog.DuplicateKeys.Add(
                new LocalizationDuplicateKey("en-US", "Core", "key", "a", "b"));
            var diagnostics = new StubDiagnostics();
            var service = new LocalizationService(
                new StubSource(catalog),
                new StubLanguages("en-US", "en-US"),
                diagnostics);

            service.GetAvailableLanguages();

            Assert.That(diagnostics.Duplicates.Count, Is.EqualTo(1));
        }

        private static LocalizationCatalog CreateCatalog(
            params (string Locale, string Scope, string Key, string Value)[] entries)
        {
            var catalog = new LocalizationCatalog();
            foreach (var item in entries)
            {
                if (!catalog.Locales.TryGetValue(item.Locale, out var locale))
                {
                    locale = new LocalizationLocaleCatalog(item.Locale);
                    catalog.Locales.Add(item.Locale, locale);
                }

                if (!locale.Scopes.TryGetValue(item.Scope, out var scope))
                {
                    scope = new LocalizationScopeCatalog(item.Scope);
                    locale.Scopes.Add(item.Scope, scope);
                }

                scope.Entries[item.Key] = new LocalizationEntry(
                    item.Locale,
                    item.Scope,
                    item.Key,
                    item.Value,
                    string.Empty);
            }

            return catalog;
        }

        private sealed class StubSource : ILocalizationCatalogSource
        {
            public StubSource(LocalizationCatalog catalog)
            {
                Catalog = catalog;
            }

            public LocalizationCatalog Catalog { get; set; }

            public int LoadCount { get; private set; }

            public LocalizationCatalog Load()
            {
                LoadCount++;
                return Catalog;
            }
        }

        private sealed class StubLanguages : ILocalizationLanguageProvider
        {
            public StubLanguages(string current, string fallback)
            {
                CurrentLanguage = current;
                FallbackLanguage = fallback;
            }

            public string CurrentLanguage { get; }

            public string FallbackLanguage { get; }
        }

        private sealed class StubDiagnostics : ILocalizationDiagnostics
        {
            public List<LocalizationDuplicateKey> Duplicates { get; } =
                new List<LocalizationDuplicateKey>();

            public void ReportDuplicates(IReadOnlyList<LocalizationDuplicateKey> duplicates)
            {
                Duplicates.AddRange(duplicates);
            }
        }
    }
}
