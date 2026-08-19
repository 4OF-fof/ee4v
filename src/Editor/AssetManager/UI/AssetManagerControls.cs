using System;
using System.Collections.Generic;
using Ee4v.AssetManager.Contracts;
using Ee4v.Core.EditorIntegration;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal static class AssetManagerControls
    {
        private const string DangerActionClassName =
            "ee4v-asset-manager__danger-action";
        private const string FluentIconDirectory =
            "/Editor/ThirdParty/FluentUiSystemIcons/" +
            "Png512/";

        public static UiButton CreateButton(
            string text = "",
            Action onClick = null,
            params string[] classNames)
        {
            var button = new UiButton(text, onClick);
            AddClasses(button, classNames);
            return button;
        }

        public static UiButton CreateDangerButton(
            string text,
            Action onClick = null,
            params string[] classNames)
        {
            var button = CreateButton(text, onClick, classNames);
            button.AddToClassList(DangerActionClassName);
            button.SetLabelColor(UiColorTokens.StatusFailedText);
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

        public static UiButton CreateNavigationButton(
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
                    UiSizeTokens.Size12),
                labelTypographyClassName:
                    UiClassNames.NavigationItemLabel);
            AddClasses(button, classNames);
            return button;
        }

        public static void SetNavigationSelected(
            UiButton button,
            bool selected)
        {
            if (button == null)
            {
                return;
            }

            button.EnableInClassList(
                "ee4v-asset-manager__nav-button--selected",
                selected);
            button.SetLabelColor(
                selected
                    ? UiColorTokens.TextOnState
                    : UiColorTokens.TextPrimary);
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
            string[] classNames)
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
                UiButtonVariant.Solid,
                compact: true);
            AddClasses(button, classNames);

            return button;
        }

        internal static Texture2D LoadFluentIconTexture(
            string iconFileName)
        {
            var packageRoot = PackageAssetApi.GetPackageRootAssetPath();
            return string.IsNullOrEmpty(packageRoot) ||
                   string.IsNullOrEmpty(iconFileName)
                ? null
                : AssetDatabase.LoadAssetAtPath<Texture2D>(
                    packageRoot + FluentIconDirectory + iconFileName);
        }

        internal static IconState LoadFluentIconState(
            string iconFileName,
            float size,
            string tooltip = null,
            Color? tintColor = null)
        {
            var texture = LoadFluentIconTexture(iconFileName);
            return texture == null
                ? null
                : IconState.FromTexture(
                    texture,
                    size,
                    tooltip,
                    tintColor ?? UiColorTokens.TextPrimary);
        }

        public static AssetManagerTextField CreateTextField(
            string label = "",
            params string[] classNames)
        {
            return new AssetManagerTextField(label, classNames);
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

        public static AssetManagerGridSizeSlider CreateGridSizeSlider(
            int value,
            int minimum = 1,
            int maximum = 12,
            params string[] classNames)
        {
            return new AssetManagerGridSizeSlider(
                value,
                minimum,
                maximum,
                classNames);
        }

        public static AssetManagerEnumField CreateEnumField(
            string label,
            Enum value,
            Func<Enum, string> formatter = null,
            params string[] classNames)
        {
            return new AssetManagerEnumField(
                label,
                value,
                formatter,
                classNames);
        }

        public static string FormatFilterCondition(Enum value)
        {
            if (!(value is AssetFilterConditionType condition))
            {
                return value?.ToString() ?? string.Empty;
            }

            switch (condition)
            {
                case AssetFilterConditionType.DescriptionContains:
                    return I18N.Get("filterCondition.descriptionContains");
                case AssetFilterConditionType.HasTag:
                    return I18N.Get("filterCondition.hasTag");
                case AssetFilterConditionType.HasFileExtension:
                    return I18N.Get("filterCondition.hasFileExtension");
                default:
                    return I18N.Get("filterCondition.nameContains");
            }
        }

        public static AssetManagerFoldout CreateFoldout(
            string text,
            bool value = false,
            params string[] classNames)
        {
            return new AssetManagerFoldout(text, value, classNames);
        }

        public static AssetManagerNotice CreateNotice(
            string text,
            params string[] classNames)
        {
            return new AssetManagerNotice(text, classNames);
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

    internal sealed class AssetManagerTextField : VisualElement
    {
        private readonly InputField _field;
        private bool _multiline;

        public AssetManagerTextField(
            string label,
            params string[] classNames)
        {
            AddToClassList("ee4v-asset-manager-control-field");
            AssetManagerControls.AddClasses(this, classNames);
            if (!string.IsNullOrWhiteSpace(label))
            {
                hierarchy.Add(UiTextFactory.Create(
                    label,
                    UiClassNames.FormLabel,
                    "ee4v-asset-manager-control-field__label"));
            }

            _field = new InputField();
            _field.AddToClassList(
                "ee4v-asset-manager-control-field__input");
            hierarchy.Add(_field);
        }

        public string value
        {
            get { return _field.Value; }
            set { _field.Value = value ?? string.Empty; }
        }

        public bool multiline
        {
            get { return _multiline; }
            set
            {
                _multiline = value;
                _field.SetState(new InputFieldState(
                    _field.Value,
                    value));
            }
        }

        public bool isReadOnly
        {
            get { return _field.IsReadOnly; }
            set { _field.IsReadOnly = value; }
        }

        public void FocusInput()
        {
            _field.FocusInput();
        }
    }

    internal sealed class AssetManagerGridSizeSlider : VisualElement
    {
        private readonly SliderInt _slider;

        public AssetManagerGridSizeSlider(
            int value,
            int minimum,
            int maximum,
            params string[] classNames)
        {
            var safeMinimum = Math.Min(minimum, maximum);
            var safeMaximum = Math.Max(minimum, maximum);
            AddToClassList("ee4v-asset-manager-grid-slider");
            AssetManagerControls.AddClasses(this, classNames);

            hierarchy.Add(CreateEndpointButton(
                I18N.Get("toolbar.gridDecrease"),
                "subtract.png",
                -1));
            _slider = new SliderInt(safeMinimum, safeMaximum)
            {
                showInputField = false
            };
            _slider.AddToClassList(
                "ee4v-asset-manager-grid-slider__slider");
            _slider.SetValueWithoutNotify(Mathf.Clamp(
                value,
                safeMinimum,
                safeMaximum));
            _slider.RegisterValueChangedCallback(evt =>
                ValueChanged?.Invoke(evt.newValue));
            hierarchy.Add(_slider);
            hierarchy.Add(CreateEndpointButton(
                I18N.Get("toolbar.gridIncrease"),
                "add.png",
                1));
        }

        public event Action<int> ValueChanged;

        public void SetRangeWithoutNotify(int minimum, int maximum)
        {
            var safeMinimum = Math.Min(minimum, maximum);
            var safeMaximum = Math.Max(minimum, maximum);
            _slider.lowValue = safeMinimum;
            _slider.highValue = safeMaximum;
            SetValueWithoutNotify(_slider.value);
        }

        public void SetValueWithoutNotify(int value)
        {
            _slider.SetValueWithoutNotify(Mathf.Clamp(
                value,
                _slider.lowValue,
                _slider.highValue));
        }

        private void SetValue(int value)
        {
            _slider.value = Mathf.Clamp(
                value,
                _slider.lowValue,
                _slider.highValue);
        }

        private UiButton CreateEndpointButton(
            string tooltip,
            string iconFileName,
            int delta)
        {
            return AssetManagerControls.CreateIconButton(
                tooltip,
                iconFileName,
                () => SetValue(_slider.value + delta),
                "ee4v-asset-manager-grid-slider__endpoint");
        }
    }

    internal sealed class AssetManagerEnumField : VisualElement
    {
        private readonly BaseField<Enum> _field;

        public AssetManagerEnumField(
            string label,
            Enum value,
            Func<Enum, string> formatter,
            params string[] classNames)
        {
            AddToClassList("ee4v-asset-manager-control-field");
            AddToClassList("ee4v-asset-manager-control-field--enum");
            AssetManagerControls.AddClasses(this, classNames);
            if (!string.IsNullOrWhiteSpace(label))
            {
                hierarchy.Add(UiTextFactory.Create(
                    label,
                    UiClassNames.FormLabel,
                    "ee4v-asset-manager-control-field__label"));
            }

            var fieldContainer = new VisualElement();
            fieldContainer.AddToClassList(
                "ee4v-asset-manager-control-field__container");
            if (formatter == null)
            {
                _field = UiTextFactory.CreateEnumField(
                    string.Empty,
                    value,
                    "ee4v-asset-manager-control-field__input");
            }
            else
            {
                var values = Enum.GetValues(value.GetType());
                var choices = new List<Enum>(values.Length);
                var selectedIndex = 0;
                for (var i = 0; i < values.Length; i++)
                {
                    var choice = (Enum)values.GetValue(i);
                    choices.Add(choice);
                    if (choice.Equals(value))
                    {
                        selectedIndex = i;
                    }
                }

                _field = UiTextFactory.CreatePopupField(
                    string.Empty,
                    choices,
                    selectedIndex,
                    formatter,
                    formatter,
                    "ee4v-asset-manager-control-field__input");
            }
            _field.RegisterCallback<FocusInEvent>(_ => EnableInClassList(
                "ee4v-asset-manager-control-field--focused",
                true));
            _field.RegisterCallback<FocusOutEvent>(_ => EnableInClassList(
                "ee4v-asset-manager-control-field--focused",
                false));
            fieldContainer.Add(_field);
            hierarchy.Add(fieldContainer);
        }

        public Enum value
        {
            get { return _field.value; }
        }

        public void SetValueWithoutNotify(Enum value)
        {
            _field.SetValueWithoutNotify(value);
        }
    }

    internal sealed class AssetManagerFoldout : VisualElement
    {
        private readonly UiButton _header;
        private readonly VisualElement _body;
        private readonly string _text;
        private bool _value;

        public AssetManagerFoldout(
            string text,
            bool value,
            params string[] classNames)
        {
            _text = text ?? string.Empty;
            AddToClassList("ee4v-asset-manager-control-foldout");
            AssetManagerControls.AddClasses(this, classNames);

            _header = AssetManagerControls.CreateButton(
                string.Empty,
                Toggle,
                "ee4v-asset-manager-control-foldout__header");
            _body = new VisualElement();
            _body.AddToClassList(
                "ee4v-asset-manager-control-foldout__body");
            hierarchy.Add(_header);
            hierarchy.Add(_body);
            SetExpanded(value);
        }

        public override VisualElement contentContainer => _body;

        private void Toggle()
        {
            SetExpanded(!_value);
        }

        private void SetExpanded(bool expanded)
        {
            _value = expanded;
            _header.SetLabel(_text);
            _header.SetIcon(
                AssetManagerControls.LoadFluentIconState(
                    expanded
                        ? "chevron_down.png"
                        : "chevron_right.png",
                    UiSizeTokens.Size12));
            _body.style.display = expanded
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            EnableInClassList(
                "ee4v-asset-manager-control-foldout--expanded",
                expanded);
        }
    }

    internal sealed class AssetManagerNotice : VisualElement
    {
        public AssetManagerNotice(
            string text,
            params string[] classNames)
        {
            AddToClassList("ee4v-asset-manager-control-notice");
            AssetManagerControls.AddClasses(this, classNames);
            var icon = AssetManagerControls.CreateIcon(
                "info.png",
                UiSizeTokens.Size18);
            if (icon != null)
            {
                icon.AddToClassList(
                    "ee4v-asset-manager-control-notice__icon");
                Add(icon);
            }
            Add(UiTextFactory.Create(
                text,
                "ee4v-asset-manager-control-notice__text"));
        }
    }

}
