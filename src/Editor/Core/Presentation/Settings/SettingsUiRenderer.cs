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
        private const string SectionLabelClassName =
            "ee4v-settings__section-label";
        private const string RowClassName = "ee4v-settings__row";
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
            root.AddToClassList("ee4v-ui");
            root.AddToClassList(RootClassName);
            root.style.flexGrow = 1f;
            root.style.minHeight = 0f;
            UiStyleUtility.AddPackageStyleSheet(
                root,
                "Editor/UI/Components/common.uss");
            UiStyleUtility.AddPackageStyleSheet(
                root,
                "Editor/UI/Components/Inputs/InputField/input-field.uss");
            UiStyleUtility.AddPackageStyleSheet(
                root,
                "Editor/UI/Components/Inputs/CommaSeparatedListField/comma-separated-list-field.uss");
            UiStyleUtility.AddPackageStyleSheet(
                root,
                "Editor/UI/Components/Inputs/ReorderableListField/reorderable-list-field.uss");
            UiStyleUtility.AddPackageStyleSheet(
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
                var section = new Foldout { value = true };
                section.AddToClassList(SectionClassName);
                UiTextFactory.AttachToFoldout(
                    section,
                    Translate(
                        firstDefinition.SectionKey,
                        firstDefinition.LocalizationScope),
                    UiClassNames.SectionTitle,
                    SectionLabelClassName);

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
            row.AddToClassList(RowClassName);

            var errorBox = new HelpBox(string.Empty, HelpBoxMessageType.Error);
            errorBox.style.marginTop = UiSpacingTokens.Xxs;

            var labelText = Translate(
                definition.DisplayNameKey,
                definition.LocalizationScope);
            var label = UiTextFactory.Create(labelText);
            label.AddToClassList(LabelClassName);
            label.tooltip = tooltip;

            var field = SettingDrawerRegistry.Create(
                definition,
                tooltip,
                settings.Get(definition),
                searchContext,
                value => ApplyValue(settings, definition, value, errorBox));
            field.AddToClassList(FieldClassName);

            var fieldLayout = new VisualElement();
            fieldLayout.AddToClassList(FieldLayoutClassName);
            fieldLayout.Add(label);
            fieldLayout.Add(field);
            row.Add(fieldLayout);
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
