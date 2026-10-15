using System;
using System.Collections.Generic;
using Ee4v.AssetManager.Contracts;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal static class AssetManagerControls
    {
        private const string PrimaryActionClassName =
            "ee4v-asset-manager__primary-action";
        private const string DangerActionClassName =
            "ee4v-asset-manager__danger-action";
        private const string IconButtonClassName =
            "ee4v-asset-manager__icon-button";

        public static EmptyState CreateEmptyState(string text, string iconFileName = "info.png")
        {
            var empty = new EmptyState(new EmptyStateState(
                string.Empty,
                text,
                LoadFluentIconState(iconFileName, 48f,
                    tintColor: UiColorTokens.TextMuted)));
            empty.AddToClassList("ee4v-asset-manager__empty-state");
            return empty;
        }

        public static UiButton CreateButton(
            string text = "",
            Action onClick = null,
            params string[] classNames)
        {
            var button = new UiButton(text, onClick);
            AddClasses(button, classNames);
            if (button.ClassListContains(PrimaryActionClassName))
            {
                button.SetTone(UiButtonTone.Primary);
            }
            return button;
        }

        public static UiButton CreateDangerButton(
            string text,
            Action onClick = null,
            params string[] classNames)
        {
            var button = CreateButton(text, onClick, classNames);
            button.AddToClassList(DangerActionClassName);
            button.SetTone(UiButtonTone.Danger);
            return button;
        }

        public static UiButton CreateReloadButton(
            Action onClick = null,
            params string[] classNames)
        {
            return CreateFluentIconButton(
                I18N.Get("toolbar.reload"),
                "arrow_clockwise.png",
                UiSizeTokens.Size12,
                onClick,
                classNames);
        }

        public static UiButton CreateSortButton(
            Action onClick = null,
            params string[] classNames)
        {
            return CreateFluentIconButton(
                I18N.Get("toolbar.sort.button"),
                "arrow_sort.png",
                UiSizeTokens.Size12,
                onClick,
                classNames);
        }

        public static UiButton CreateIconButton(
            string tooltip,
            string iconFileName,
            Action onClick = null,
            params string[] classNames)
        {
            return CreateFluentIconButton(
                tooltip,
                iconFileName,
                UiSizeTokens.Size12,
                onClick,
                classNames);
        }

        public static UiButton CreateIconButton(
            string tooltip,
            string iconFileName,
            float iconSize,
            UiButtonVariant variant,
            Action onClick = null,
            params string[] classNames)
        {
            return CreateFluentIconButton(
                tooltip,
                iconFileName,
                iconSize,
                onClick,
                classNames,
                variant);
        }

        public static UiButton CreateIconTextButton(
            string text,
            string iconFileName,
            Action onClick = null,
            params string[] classNames)
        {
            var button = new UiButton(
                text,
                onClick,
                icon: LoadFluentIconState(
                    iconFileName,
                    UiSizeTokens.Size12));
            AddClasses(button, classNames);
            return button;
        }

        public static string GetCollectionIconFileName(AssetCollectionIcon icon)
        {
            return icon == AssetCollectionIcon.File
                ? "document.png"
                : icon.ToString().ToLowerInvariant() + ".png";
        }

        public static NavigationItem CreateNavigationButton(
            string text,
            string iconFileName,
            Action onClick = null,
            params string[] classNames)
        {
            var button = new NavigationItem(
                new NavigationItemState(
                    text,
                    icon: LoadFluentIconState(
                        iconFileName,
                        UiSizeTokens.Size14)),
                onClick);
            AddClasses(button, classNames);
            return button;
        }

        public static void SetNavigationSelected(
            NavigationItem button,
            bool selected)
        {
            if (button == null)
            {
                return;
            }

            button.SetSelected(selected);
        }

        public static Icon CreateIcon(
            string iconFileName,
            float size,
            Color? tintColor = null)
        {
            var state = LoadFluentIconState(
                iconFileName,
                size,
                tintColor: tintColor);
            if (state == null)
            {
                return null;
            }

            return new Icon(state);
        }

        private static UiButton CreateFluentIconButton(
            string tooltip,
            string iconFileName,
            float iconSize,
            Action onClick,
            string[] classNames,
            UiButtonVariant variant = UiButtonVariant.Solid)
        {
            var icon = LoadFluentIconState(
                iconFileName,
                iconSize,
                tooltip);
            var button = new UiButton(
                icon == null ? tooltip : string.Empty,
                onClick,
                tooltip,
                icon,
                variant);
            if (icon != null)
            {
                button.AddToClassList(IconButtonClassName);
            }
            AddClasses(button, classNames);

            return button;
        }

        internal static Texture2D LoadFluentIconTexture(
            string iconFileName,
            float size = UiSizeTokens.Size16)
        {
            return FluentUiIcons.LoadTexture(iconFileName, size);
        }

        internal static IconState LoadFluentIconState(
            string iconFileName,
            float size,
            string tooltip = null,
            Color? tintColor = null)
        {
            return FluentUiIcons.CreateState(
                iconFileName,
                size,
                tooltip,
                tintColor);
        }

        public static FormInput<InputField> CreateTextField(
            string label = "",
            params string[] classNames)
        {
            var field = new FormInput<InputField>(label, new InputField());
            field.AddToClassList("ee4v-asset-manager-control-field");
            AddClasses(field, classNames);
            return field;
        }

        public static SearchField CreateSearchField(
            string placeholder = "",
            bool searchActionEnabled = false,
            params string[] classNames)
        {
            var field = new SearchField(new SearchFieldState(
                placeholder: placeholder,
                searchTooltip: I18N.Get("toolbar.search.options"),
                clearTooltip: I18N.Get("toolbar.search.clear"),
                searchIconState: LoadFluentIconState(
                    "search.png",
                    UiSizeTokens.Size14),
                clearIconState: LoadFluentIconState(
                    "dismiss.png",
                    UiSizeTokens.Size10),
                searchActionEnabled: searchActionEnabled));
            AddClasses(field, classNames);
            return field;
        }

        public static StepperSlider CreateGridSizeSlider(
            int value,
            int minimum = 1,
            int maximum = 12,
            params string[] classNames)
        {
            return new StepperSlider(
                value,
                minimum,
                maximum,
                classNames);
        }

        public static EnumFormInput CreateEnumField(
            string label,
            Enum value,
            Func<Enum, string> formatter = null,
            params string[] classNames)
        {
            return new EnumFormInput(
                label,
                value,
                formatter,
                classNames);
        }

        public static string FormatFilterCondition(Enum value)
        {
            return FormatFilterCondition(value, false);
        }

        public static string FormatFilterCondition(Enum value, bool inverted)
        {
            if (!(value is AssetFilterConditionType condition))
            {
                return value?.ToString() ?? string.Empty;
            }

            string key;
            switch (condition)
            {
                case AssetFilterConditionType.DescriptionContains:
                    key = "descriptionContains";
                    break;
                case AssetFilterConditionType.HasTag:
                    key = "hasTag";
                    break;
                case AssetFilterConditionType.HasFileExtension:
                    key = "hasFileExtension";
                    break;
                default:
                    key = "nameContains";
                    break;
            }
            return I18N.Get("filterCondition." + key + (inverted ? "Inverted" : string.Empty));
        }

        public static MessagePanel CreateNotice(
            string text,
            params string[] classNames)
        {
            var panel = new MessagePanel(new MessagePanelState(string.Empty, text, MessageSeverity.Info),
                MessagePanelDensity.Compact);
            AddClasses(panel, classNames);
            return panel;
        }

        internal static void AddClasses(
            VisualElement element,
            string[] classNames)
        {
            if (classNames == null)
            {
                return;
            }

            for (var i = 0; i < classNames.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(classNames[i]))
                {
                    element.AddToClassList(classNames[i]);
                }
            }
        }

    }

}
