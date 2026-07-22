using System;
using System.Linq;
using Ee4v.Core.Settings;
using UnityEditor;
using UnityEngine.UIElements;

namespace Ee4v.Core.I18n
{
    [InitializeOnLoad]
    internal static class CoreLocalizationDefinitions
    {
        private static bool _registered;

        public static readonly SettingDefinition<string> Language = new SettingDefinition<string>(
            "core.i18n.language",
            SettingScope.User,
            "settings.section.localization",
            "settings.language.label",
            "settings.language.tooltip",
            "ja-JP",
            order: 0,
            validator: ValidateLocale,
            customDrawer: DrawLocaleField());

        public static readonly SettingDefinition<string> FallbackLanguage = new SettingDefinition<string>(
            "core.i18n.fallbackLanguage",
            SettingScope.User,
            "settings.section.localization",
            "settings.fallbackLanguage.label",
            "settings.fallbackLanguage.tooltip",
            "en-US",
            order: 1,
            validator: ValidateLocale,
            customDrawer: DrawLocaleField());

        static CoreLocalizationDefinitions()
        {
            RegisterAll();
        }

        public static void RegisterAll()
        {
            if (_registered)
            {
                return;
            }

            _registered = true;
            SettingApi.Register(Language);
            SettingApi.Register(FallbackLanguage);

            SettingApi.Changed -= OnSettingChanged;
            SettingApi.Changed += OnSettingChanged;
        }

        private static void OnSettingChanged(SettingDefinitionBase definition, object value)
        {
            if (definition == Language || definition == FallbackLanguage)
            {
                I18N.Reload();
            }
        }

        private static SettingValidationResult ValidateLocale(string locale)
        {
            return string.IsNullOrWhiteSpace(locale)
                ? SettingValidationResult.Error(I18N.Get("settings.validation.locale"))
                : SettingValidationResult.Success;
        }

        private static Func<SettingDrawerContext<string>, VisualElement> DrawLocaleField()
        {
            return context =>
            {
                var languages = I18N.GetAvailableLanguages();
                if (languages.Count == 0)
                {
                    var textField = new TextField(context.Label)
                    {
                        tooltip = context.Tooltip,
                        value = context.Value ?? string.Empty
                    };
                    textField.RegisterValueChangedCallback(evt => context.NotifyValueChanged(evt.newValue));
                    return textField;
                }

                var options = languages.ToList();
                var currentIndex = Math.Max(0, options.IndexOf(context.Value));
                var popup = new PopupField<string>(context.Label, options, currentIndex)
                {
                    tooltip = context.Tooltip
                };
                popup.RegisterValueChangedCallback(evt => context.NotifyValueChanged(evt.newValue));
                return popup;
            };
        }
    }
}
