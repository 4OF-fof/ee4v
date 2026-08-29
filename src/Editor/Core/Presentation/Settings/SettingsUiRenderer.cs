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
        private const string RootClassName = "ee4v-settings";
        private const string ContentClassName = "ee4v-settings__content";
        private const string SectionClassName = "ee4v-settings__section";
        private const string ErrorClassName = "ee4v-settings__error";
        private const string FieldLayoutClassName =
            "ee4v-settings__field-layout";
        private const string LabelClassName =
            "ee4v-settings__label";
        private const string FieldClassName = "ee4v-settings__field";
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
            root.AddToClassList(RootClassName);
            root.style.flexGrow = 1f;
            root.style.minHeight = 0f;
            UiComposition.Prepare(
                root,
                "Editor/Core/Presentation/Settings/settings-ui.uss");

            var scrollView = new ScrollView(ScrollViewMode.Vertical);
            scrollView.style.flexGrow = 1f;
            scrollView.style.minHeight = 0f;
            scrollView.verticalScrollerVisibility = ScrollerVisibility.Auto;

            var content = scrollView.contentContainer;
            content.AddToClassList(ContentClassName);
            content.style.paddingLeft = UiSpacingTokens.Small;
            content.style.paddingRight = UiSpacingTokens.Small;
            content.style.paddingTop = UiSpacingTokens.Small;
            content.style.paddingBottom = UiSpacingTokens.Small;
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
                var firstInput = CreateDefinition(
                    settings,
                    firstDefinition,
                    searchContext,
                    out var firstErrorBox);
                var section = new InputGroup(
                    Translate(
                        firstDefinition.SectionKey,
                        firstDefinition.LocalizationScope),
                    firstInput);
                section.AddToClassList(SectionClassName);
                section.Add(firstErrorBox);

                for (var index = 1;
                     index < visibleDefinitions.Length;
                     index++)
                {
                    var input = CreateDefinition(
                        settings,
                        visibleDefinitions[index],
                        searchContext,
                        out var errorBox);
                    section.AddInput(input);
                    section.Add(errorBox);
                }

                content.Add(section);
            }
        }

        private static FormInput CreateDefinition(
            ISettingsService settings,
            SettingDefinitionBase definition,
            string searchContext,
            out HelpBox errorBox)
        {
            var tooltip = string.Empty;
            if (I18N.TryGetForScope(
                    definition.LocalizationScope,
                    definition.DescriptionKey,
                    out var translatedTooltip))
            {
                tooltip = translatedTooltip;
            }

            var validationBox = UiTextFactory.CreateHelpBox(
                string.Empty,
                HelpBoxMessageType.Error);
            validationBox.AddToClassList(ErrorClassName);
            errorBox = validationBox;

            var labelText = Translate(
                definition.DisplayNameKey,
                definition.LocalizationScope);
            var field = SettingDrawerApi.Create(
                definition,
                tooltip,
                settings.Get(definition),
                searchContext,
                value => ApplyValue(
                    settings,
                    definition,
                    value,
                    validationBox));
            field.AddToClassList(FieldClassName);

            var fieldLayout = new FormInput(labelText, field);
            fieldLayout.AddToClassList(FieldLayoutClassName);
            fieldLayout.LabelText.AddToClassList(LabelClassName);
            fieldLayout.LabelText.tooltip = tooltip;
            if (ValidationMessages.TryGetValue(definition.Key, out var error))
            {
                ShowError(validationBox, error);
            }
            else
            {
                HideError(validationBox);
            }

            return fieldLayout;
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
            UiTextFactory.SetText(errorBox, error);
            errorBox.style.display = DisplayStyle.Flex;
        }

        private static void HideError(HelpBox errorBox)
        {
            UiTextFactory.SetText(errorBox, string.Empty);
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
