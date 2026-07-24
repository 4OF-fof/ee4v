using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEngine.UIElements;

namespace Ee4v.Core.Settings
{
    internal static class SettingsUiRenderer
    {
        private static readonly Dictionary<string, string> ValidationMessages =
            new Dictionary<string, string>();

        public static void BuildScope(
            VisualElement root,
            ISettingsService settings,
            SettingScope scope,
            string searchContext)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            root.Clear();
            root.style.flexGrow = 1f;
            root.style.minHeight = 0f;

            var scrollView = new ScrollView(ScrollViewMode.Vertical);
            scrollView.style.flexGrow = 1f;
            scrollView.style.minHeight = 0f;
            scrollView.verticalScrollerVisibility = ScrollerVisibility.Auto;

            var content = scrollView.contentContainer;
            content.style.paddingLeft = UiSpacingTokens.Medium;
            content.style.paddingRight = UiSpacingTokens.Medium;
            content.style.paddingTop = UiSpacingTokens.Medium;
            content.style.paddingBottom = UiSpacingTokens.Medium;
            root.Add(scrollView);

            var grouped = settings.GetDefinitions(scope)
                .GroupBy(GetGroupKey)
                .OrderBy(group => group.Key, StringComparer.Ordinal);

            foreach (var group in grouped)
            {
                var visibleDefinitions = group
                    .Where(definition => MatchesSearch(definition, searchContext))
                    .ToArray();
                if (visibleDefinitions.Length == 0)
                {
                    continue;
                }

                var firstDefinition = visibleDefinitions[0];
                var section = new Foldout
                {
                    text = Translate(
                        firstDefinition.SectionKey,
                        firstDefinition.LocalizationScope),
                    value = true
                };
                section.style.flexShrink = 0f;
                section.style.marginBottom = UiSpacingTokens.Medium;

                foreach (var definition in visibleDefinitions)
                {
                    section.Add(CreateDefinition(settings, definition, searchContext));
                }

                content.Add(section);
            }
        }

        private static VisualElement CreateDefinition(
            ISettingsService settings,
            SettingDefinitionBase definition,
            string searchContext)
        {
            var tooltip = string.Empty;
            if (I18N.TryGetForScope(
                    definition.LocalizationScope,
                    definition.DescriptionKey,
                    out var translatedTooltip))
            {
                tooltip = translatedTooltip;
            }

            var row = new VisualElement();
            row.style.flexShrink = 0f;
            row.style.marginTop = UiSpacingTokens.Xxs;
            row.style.marginBottom = UiSpacingTokens.Xxs;

            var errorBox = new HelpBox(string.Empty, HelpBoxMessageType.Error);
            errorBox.style.marginTop = UiSpacingTokens.Xxs;

            var field = SettingDrawerRegistry.Create(
                definition,
                Translate(definition.DisplayNameKey, definition.LocalizationScope),
                tooltip,
                settings.Get(definition),
                searchContext,
                value => ApplyValue(settings, definition, value, errorBox));

            row.Add(field);
            row.Add(errorBox);

            if (ValidationMessages.TryGetValue(definition.Key, out var error))
            {
                ShowError(errorBox, error);
            }
            else
            {
                HideError(errorBox);
            }

            return row;
        }

        private static void ApplyValue(
            ISettingsService settings,
            SettingDefinitionBase definition,
            object value,
            HelpBox errorBox)
        {
            var validation = definition.Validate(value);
            if (!validation.IsValid)
            {
                ValidationMessages[definition.Key] = validation.Message;
                ShowError(errorBox, validation.Message);
                return;
            }

            ValidationMessages.Remove(definition.Key);
            HideError(errorBox);
            settings.Set(definition, value);
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

        private static bool MatchesSearch(
            SettingDefinitionBase definition,
            string searchContext)
        {
            if (string.IsNullOrWhiteSpace(searchContext))
            {
                return true;
            }

            var needle = searchContext.Trim();
            if (ContainsIgnoreCase(
                    Translate(definition.DisplayNameKey, definition.LocalizationScope),
                    needle) ||
                ContainsIgnoreCase(
                    Translate(definition.SectionKey, definition.LocalizationScope),
                    needle))
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
            return string.IsNullOrEmpty(key)
                ? string.Empty
                : I18N.GetForScope(localizationScope, key);
        }
    }
}
