using System;
using System.Linq;
using Ee4v.Core.I18n;
using UnityEditor;
using UnityEngine.UIElements;

namespace Ee4v.Core.Settings
{
    [InitializeOnLoad]
    internal static class CoreLocalizationSettingDrawers
    {
        static CoreLocalizationSettingDrawers()
        {
            SettingDrawerRegistry.Register(
                CoreLocalizationDefinitions.Language,
                DrawLocaleField);
            SettingDrawerRegistry.Register(
                CoreLocalizationDefinitions.FallbackLanguage,
                DrawLocaleField);
        }

        private static VisualElement DrawLocaleField(SettingDrawerContext<string> context)
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
        }
    }
}
