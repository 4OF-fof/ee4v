using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.I18n;
using UnityEngine.UIElements;

namespace Ee4v.Core.Settings
{
    internal static class SettingsUiRenderer
    {
        private static readonly Dictionary<string, string> ValidationMessages = new Dictionary<string, string>();

        public static void BuildScope(VisualElement root, SettingScope scope, string searchContext)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            root.Clear();
            root.style.paddingLeft = 8f;
            root.style.paddingRight = 8f;
            root.style.paddingTop = 8f;
            root.style.paddingBottom = 8f;

            var definitions = SettingApi.GetDefinitions(scope);
            var grouped = definitions
                .GroupBy(definition => GetGroupKey(definition))
                .OrderBy(group => group.Key, StringComparer.Ordinal);

            foreach (var group in grouped)
            {
                var visibleDefinitions = group.Where(definition => MatchesSearch(definition, searchContext)).ToArray();
                if (visibleDefinitions.Length == 0)
                {
                    continue;
                }

                var firstDefinition = visibleDefinitions[0];
                var section = new Foldout
                {
                    text = Translate(firstDefinition.SectionKey, firstDefinition.LocalizationScope),
                    value = true
                };
                section.style.marginBottom = 8f;

                for (var i = 0; i < visibleDefinitions.Length; i++)
                {
                    section.Add(CreateDefinition(visibleDefinitions[i], searchContext));
                }

                root.Add(section);
            }
        }

        private static VisualElement CreateDefinition(SettingDefinitionBase definition, string searchContext)
        {
            var tooltip = string.Empty;
            string translatedTooltip;
            if (I18N.TryGetForScope(definition.LocalizationScope, definition.DescriptionKey, out translatedTooltip))
            {
                tooltip = translatedTooltip;
            }

            var row = new VisualElement();
            row.style.marginTop = 2f;
            row.style.marginBottom = 2f;

            var errorBox = new HelpBox(string.Empty, HelpBoxMessageType.Error);
            errorBox.style.marginTop = 2f;

            var label = Translate(definition.DisplayNameKey, definition.LocalizationScope);
            var currentValue = SettingApi.GetBoxed(definition);
            var field = definition.CreateField(
                label,
                tooltip,
                currentValue,
                searchContext,
                value => ApplyValue(definition, value, errorBox));

            row.Add(field);
            row.Add(errorBox);

            string error;
            if (ValidationMessages.TryGetValue(definition.Key, out error))
            {
                ShowError(errorBox, error);
            }
            else
            {
                HideError(errorBox);
            }

            return row;
        }

        private static void ApplyValue(SettingDefinitionBase definition, object value, HelpBox errorBox)
        {
            var validation = definition.ValidateBoxed(value);
            if (!validation.IsValid)
            {
                ValidationMessages[definition.Key] = validation.Message;
                ShowError(errorBox, validation.Message);
                return;
            }

            ValidationMessages.Remove(definition.Key);
            HideError(errorBox);
            SettingApi.SetBoxed(definition, value);
        }

        private static void ShowError(HelpBox errorBox, string error)
        {
            errorBox.text = error ?? string.Empty;
            errorBox.style.display = DisplayStyle.Flex;
        }

        private static void HideError(HelpBox errorBox)
        {
            errorBox.text = string.Empty;
            errorBox.style.display = DisplayStyle.None;
        }

        private static bool MatchesSearch(SettingDefinitionBase definition, string searchContext)
        {
            if (string.IsNullOrWhiteSpace(searchContext))
            {
                return true;
            }

            var needle = searchContext.Trim();
            if (ContainsIgnoreCase(Translate(definition.DisplayNameKey, definition.LocalizationScope), needle) ||
                ContainsIgnoreCase(Translate(definition.SectionKey, definition.LocalizationScope), needle))
            {
                return true;
            }

            return definition.Keywords.Any(keyword => ContainsIgnoreCase(keyword, needle));
        }

        private static bool ContainsIgnoreCase(string source, string needle)
        {
            return !string.IsNullOrEmpty(source) &&
                   source.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string GetGroupKey(SettingDefinitionBase definition)
        {
            return definition.LocalizationScope + "|" + definition.SectionKey;
        }

        private static string Translate(string key, string localizationScope)
        {
            return string.IsNullOrEmpty(key) ? string.Empty : I18N.GetForScope(localizationScope, key);
        }
    }
}
