using System;
using System.Collections.Generic;

namespace Ee4v.Core.Localization
{
    public interface ILocalizer
    {
        string Scope { get; }

        string Get(string key, params object[] arguments);

        bool TryGet(string key, out string value);
    }

    public interface ILocalizationService
    {
        event EventHandler Reloaded;

        ILocalizer ForScope(string scope);

        IReadOnlyList<string> GetAvailableLanguages();

        void Reload();
    }

    public interface ILocalizationCatalogSource
    {
        LocalizationCatalog Load();
    }

    public interface ILocalizationLanguageProvider
    {
        string CurrentLanguage { get; }

        string FallbackLanguage { get; }
    }

    public interface ILocalizationDiagnostics
    {
        void ReportDuplicates(IReadOnlyList<LocalizationDuplicateKey> duplicates);
    }
}
