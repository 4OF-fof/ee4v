using Ee4v.Core.Settings;
using UnityEditor;

namespace Ee4v.Core.I18n
{
    [InitializeOnLoad]
    internal static class CoreLocalizationDefinitions
    {
        public static readonly SettingDefinition<string> Language = new SettingDefinition<string>(
            "core.i18n.language",
            SettingScope.User,
            "Core",
            "settings.section.localization",
            "settings.language.label",
            "settings.language.tooltip",
            "ja-JP",
            order: 0,
            validator: ValidateLocale);

        public static readonly SettingDefinition<string> FallbackLanguage = new SettingDefinition<string>(
            "core.i18n.fallbackLanguage",
            SettingScope.User,
            "Core",
            "settings.section.localization",
            "settings.fallbackLanguage.label",
            "settings.fallbackLanguage.tooltip",
            "en-US",
            order: 1,
            validator: ValidateLocale);

        static CoreLocalizationDefinitions()
        {
            RegisterAll(CoreSettings.Current);
        }

        public static void RegisterAll(ISettingsService settings)
        {
            settings.Register(Language);
            settings.Register(FallbackLanguage);

            settings.Changed -= OnSettingChanged;
            settings.Changed += OnSettingChanged;
        }

        private static void OnSettingChanged(object sender, SettingChangedEventArgs args)
        {
            if (args.Definition == Language || args.Definition == FallbackLanguage)
            {
                CoreLocalization.Current.Reload();
            }
        }

        private static SettingValidationResult ValidateLocale(string locale)
        {
            return string.IsNullOrWhiteSpace(locale)
                ? SettingValidationResult.Error(
                    CoreLocalization.Current.ForScope("Core").Get("settings.validation.locale"))
                : SettingValidationResult.Success;
        }
    }
}
