using System;
using System.Linq;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEditor;
using UnityEngine.UIElements;

namespace Ee4v.Core.Settings
{
    [InitializeOnLoad]
    internal static class CoreLocalizationSettingDrawers
    {
        static CoreLocalizationSettingDrawers()
        {
            SettingDrawerApi.Register(
                CoreLocalizationDefinitions.Language,
                DrawLocaleField);
            SettingDrawerApi.Register(
                CoreLocalizationDefinitions.FallbackLanguage,
                DrawLocaleField);
        }

        private static VisualElement DrawLocaleField(SettingDrawerContext<string> context)
        {
            var languages = I18N.GetAvailableLanguages();
            if (languages.Count == 0)
            {
                var textField = UiTextFactory.CreateTextField();
                textField.tooltip = context.Tooltip;
                textField.value = context.Value ?? string.Empty;
                textField.RegisterValueChangedCallback(evt => context.NotifyValueChanged(evt.newValue));
                return textField;
            }

            var options = languages.ToList();
            var currentIndex = Math.Max(0, options.IndexOf(context.Value));
            var popup = UiTextFactory.CreatePopupField(
                string.Empty,
                options,
                currentIndex);
            popup.tooltip = context.Tooltip;
            popup.RegisterValueChangedCallback(evt => context.NotifyValueChanged(evt.newValue));
            return popup;
        }
    }
}
