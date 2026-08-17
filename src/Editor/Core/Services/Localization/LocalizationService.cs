using System;
using System.Collections.Generic;
using System.Linq;

namespace Ee4v.Core.Localization
{
    public sealed class LocalizationService : ILocalizationService
    {
        private const string EnglishLocale = "en-US";
        private readonly ILocalizationCatalogSource _catalogSource;
        private readonly ILocalizationLanguageProvider _languageProvider;
        private readonly ILocalizationDiagnostics _diagnostics;
        private readonly Dictionary<string, ILocalizer> _localizers =
            new Dictionary<string, ILocalizer>(StringComparer.OrdinalIgnoreCase);
        private LocalizationCatalog _catalog;

        public LocalizationService(
            ILocalizationCatalogSource catalogSource,
            ILocalizationLanguageProvider languageProvider,
            ILocalizationDiagnostics diagnostics)
        {
            _catalogSource = catalogSource ?? throw new ArgumentNullException(nameof(catalogSource));
            _languageProvider = languageProvider ?? throw new ArgumentNullException(nameof(languageProvider));
            _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        }

        public event EventHandler Reloaded;

        public ILocalizer ForScope(string scope)
        {
            if (string.IsNullOrWhiteSpace(scope))
            {
                throw new ArgumentException("Localization scope is required.", nameof(scope));
            }

            if (!_localizers.TryGetValue(scope, out var localizer))
            {
                localizer = new ScopedLocalizer(this, scope);
                _localizers.Add(scope, localizer);
            }

            return localizer;
        }

        public IReadOnlyList<string> GetAvailableLanguages()
        {
            return EnsureCatalog().Locales.Keys
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public void Reload()
        {
            _catalog = null;
            Reloaded?.Invoke(this, EventArgs.Empty);
        }

        private string Get(string scope, string key, object[] arguments)
        {
            if (!TryGet(scope, key, out var resolved))
            {
                resolved = key;
            }

            if (arguments == null || arguments.Length == 0)
            {
                return resolved;
            }

            try
            {
                return string.Format(resolved, arguments);
            }
            catch (FormatException)
            {
                return key;
            }
        }

        private bool TryGet(string scope, string key, out string value)
        {
            foreach (var locale in GetFallbackSequence())
            {
                if (!EnsureCatalog().Locales.TryGetValue(locale, out var localeCatalog) ||
                    !localeCatalog.Scopes.TryGetValue(scope, out var scopeCatalog) ||
                    !scopeCatalog.Entries.TryGetValue(key, out var entry))
                {
                    continue;
                }

                value = entry.Value;
                return true;
            }

            value = null;
            return false;
        }

        private IEnumerable<string> GetFallbackSequence()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var locale in new[]
                     {
                         _languageProvider.CurrentLanguage,
                         _languageProvider.FallbackLanguage,
                         EnglishLocale
                     })
            {
                if (!string.IsNullOrWhiteSpace(locale) && seen.Add(locale))
                {
                    yield return locale;
                }
            }
        }

        private LocalizationCatalog EnsureCatalog()
        {
            if (_catalog == null)
            {
                _catalog = _catalogSource.Load() ?? new LocalizationCatalog();
                _diagnostics.ReportDuplicates(_catalog.DuplicateKeys);
            }

            return _catalog;
        }

        private sealed class ScopedLocalizer : ILocalizer
        {
            private readonly LocalizationService _owner;

            public ScopedLocalizer(LocalizationService owner, string scope)
            {
                _owner = owner;
                Scope = scope;
            }

            public string Scope { get; }

            public string Get(string key, params object[] arguments)
            {
                return _owner.Get(Scope, key, arguments);
            }

            public bool TryGet(string key, out string value)
            {
                return _owner.TryGet(Scope, key, out value);
            }
        }
    }
}
