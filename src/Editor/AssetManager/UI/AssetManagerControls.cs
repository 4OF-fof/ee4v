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
        private const string FluentIconDirectory =
            "/Editor/ThirdParty/FluentUiSystemIcons/" +
            "Png512/";

        public static AssetManagerButton CreateButton(
            string text = "",
            Action onClick = null,
            params string[] classNames)
        {
            return new AssetManagerButton(text, onClick, classNames);
        }

        public static AssetManagerButton CreateReloadButton(
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

        public static AssetManagerButton CreateSortButton(
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

        public static AssetManagerButton CreateSearchOptionsButton(
            Action onClick = null,
            params string[] classNames)
        {
            return CreateFluentIconButton(
                I18N.Get("toolbar.search.options"),
                "search.png",
                UiSizeTokens.Size12,
                onClick,
                classNames);
        }

        public static AssetManagerButton CreateIconButton(
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

        public static AssetManagerButton CreateIconButton(
            string tooltip,
            string iconFileName,
            float iconSize,
            Action onClick = null,
            params string[] classNames)
        {
            return CreateFluentIconButton(
                tooltip,
                iconFileName,
                iconSize,
                onClick,
                classNames);
        }

        public static AssetManagerButton CreateIconTextButton(
            string text,
            string iconFileName,
            Action onClick = null,
            params string[] classNames)
        {
            var button = new AssetManagerButton(
                text,
                onClick,
                classNames);
            var texture = LoadFluentIconTexture(iconFileName);
            if (texture != null)
            {
                button.AddToClassList(
                    "ee4v-asset-manager-control-button--icon-leading");
                button.SetIcon(texture, UiSizeTokens.Size12);
            }
            return button;
        }

        public static AssetManagerButton CreateBuiltinIconTextButton(
            string text,
            UiBuiltinIcon icon,
            Action onClick = null,
            params string[] classNames)
        {
            var button = new AssetManagerButton(
                text,
                onClick,
                classNames);
            button.AddToClassList(
                "ee4v-asset-manager-control-button--icon-leading");
            button.SetIcon(icon, UiSizeTokens.Size14);
            return button;
        }

        public static AssetManagerButton CreateNavigationButton(
            string text,
            UiBuiltinIcon icon,
            Action onClick = null,
            params string[] classNames)
        {
            var button = new AssetManagerButton(
                text,
                UiClassNames.NavigationItemLabel,
                onClick,
                classNames);
            button.AddToClassList(
                "ee4v-asset-manager-control-button--icon-leading");
            button.SetIcon(icon, UiSizeTokens.Size12);
            return button;
        }

        public static Image CreateIcon(
            string iconFileName,
            float size)
        {
            var texture = LoadFluentIconTexture(iconFileName);
            if (texture == null)
            {
                return null;
            }

            var icon = new Image
            {
                image = texture,
                tintColor = UiColorTokens.TextPrimary,
                scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore
            };
            icon.style.width = size;
            icon.style.height = size;
            return icon;
        }

        private static AssetManagerButton CreateFluentIconButton(
            string tooltip,
            string iconFileName,
            float iconSize,
            Action onClick,
            string[] classNames)
        {
            var texture = LoadFluentIconTexture(iconFileName);
            var button = new AssetManagerButton(
                texture == null ? tooltip : string.Empty,
                onClick,
                classNames);
            button.tooltip = tooltip;
            if (texture != null)
            {
                button.AddToClassList(
                    "ee4v-asset-manager-control-button--icon-only");
                button.SetIcon(texture, iconSize);
            }

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

        public static AssetManagerTextField CreateTextField(
            string label = "",
            params string[] classNames)
        {
            return new AssetManagerTextField(label, classNames);
        }

        public static AssetManagerSearchField CreateSearchField(
            string placeholder = "",
            params string[] classNames)
        {
            return new AssetManagerSearchField(
                placeholder,
                classNames);
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

    internal sealed class AssetManagerButton : Button
    {
        private readonly UiTextElement _label;
        private VisualElement _icon;

        public AssetManagerButton(
            string text,
            Action onClick,
            params string[] classNames)
            : this(
                text,
                UiClassNames.ButtonLabel,
                onClick,
                classNames)
        {
        }

        internal AssetManagerButton(
            string text,
            string typographyClassName,
            Action onClick,
            params string[] classNames)
            : base(onClick)
        {
            AddToClassList("ee4v-asset-manager-control-button");
            AssetManagerControls.AddClasses(this, classNames);

            _label = UiTextFactory.Create(
                text,
                typographyClassName,
                "ee4v-asset-manager-control-button__label");
            if (HasClass(classNames, "ee4v-asset-manager__danger-action"))
            {
                _label.SetColor(UiColorTokens.StatusFailedText);
            }
            _label.pickingMode = PickingMode.Ignore;
            Add(_label);
        }

        public void SetText(string text)
        {
            _label.SetText(text);
        }

        public void SetSelected(bool selected)
        {
            EnableInClassList(
                "ee4v-asset-manager__nav-button--selected",
                selected);
            _label.SetColor(
                selected
                    ? UiColorTokens.TextOnState
                    : UiColorTokens.TextPrimary);
        }

        public void SetIcon(Texture texture, float size)
        {
            if (texture == null)
            {
                _icon?.RemoveFromHierarchy();
                _icon = null;
                return;
            }

            if (!(_icon is Image image))
            {
                _icon?.RemoveFromHierarchy();
                image = new Image
                {
                    tintColor = UiColorTokens.TextPrimary,
                    scaleMode = ScaleMode.ScaleToFit,
                    pickingMode = PickingMode.Ignore
                };
                _icon = image;
                _icon.AddToClassList(
                    "ee4v-asset-manager-control-button__icon");
                hierarchy.Insert(0, _icon);
            }

            image.image = texture;
            _icon.style.width = size;
            _icon.style.height = size;
        }

        public void SetIcon(UiBuiltinIcon icon, float size)
        {
            _icon?.RemoveFromHierarchy();
            _icon = new Icon(IconState.FromBuiltinIcon(icon, size));
            _icon.AddToClassList(
                "ee4v-asset-manager-control-button__icon");
            hierarchy.Insert(0, _icon);
        }

        private static bool HasClass(string[] classNames, string target)
        {
            if (classNames == null)
            {
                return false;
            }

            for (var i = 0; i < classNames.Length; i++)
            {
                if (string.Equals(
                    classNames[i],
                    target,
                    StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }

    internal sealed class AssetManagerTextField : VisualElement
    {
        private readonly TextField _field;

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

            var fieldContainer = new VisualElement();
            fieldContainer.AddToClassList(
                "ee4v-asset-manager-control-field__container");
            _field = UiTextFactory.CreateTextField(
                string.Empty,
                "ee4v-asset-manager-control-field__input");
            _field.RegisterCallback<FocusInEvent>(_ => EnableInClassList(
                "ee4v-asset-manager-control-field--focused",
                true));
            _field.RegisterCallback<FocusOutEvent>(_ => EnableInClassList(
                "ee4v-asset-manager-control-field--focused",
                false));
            fieldContainer.Add(_field);
            hierarchy.Add(fieldContainer);
        }

        public string value
        {
            get { return _field.value; }
            set { _field.value = value ?? string.Empty; }
        }

        public bool multiline
        {
            get { return _field.multiline; }
            set
            {
                _field.multiline = value;
                EnableInClassList(
                    "ee4v-asset-manager-control-field--multiline",
                    value);
            }
        }

        public void RegisterValueChangedCallback(
            EventCallback<ChangeEvent<string>> callback)
        {
            _field.RegisterValueChangedCallback(callback);
        }

        public void FocusInput()
        {
            _field.Focus();
        }
    }

    internal sealed class AssetManagerSearchField : VisualElement
    {
        private readonly TextField _field;
        private readonly UiTextElement _placeholder;
        private readonly AssetManagerButton _clearButton;
        private readonly AssetManagerButton _optionsButton;
        private bool _isFocused;

        public AssetManagerSearchField(
            string placeholder,
            params string[] classNames)
        {
            AddToClassList("ee4v-asset-manager-control-search");
            AssetManagerControls.AddClasses(this, classNames);

            _optionsButton =
                AssetManagerControls.CreateSearchOptionsButton(
                    () => SearchOptionsClicked?.Invoke(),
                    "ee4v-asset-manager-control-search__options");

            var inputHost = new VisualElement();
            inputHost.AddToClassList(
                "ee4v-asset-manager-control-search__input-host");
            _field = UiTextFactory.CreateTextField(
                string.Empty,
                "ee4v-asset-manager-control-search__input");
            _field.RegisterValueChangedCallback(_ => RefreshState());
            _field.RegisterCallback<FocusInEvent>(_ =>
            {
                _isFocused = true;
                RefreshState();
            });
            _field.RegisterCallback<FocusOutEvent>(_ =>
            {
                _isFocused = false;
                RefreshState();
            });
            _placeholder = UiTextFactory.Create(
                placeholder,
                UiClassNames.InputPlaceholder,
                "ee4v-asset-manager-control-search__placeholder");
            _placeholder.pickingMode = PickingMode.Ignore;
            inputHost.Add(_field);
            inputHost.Add(_placeholder);

            _clearButton = AssetManagerControls.CreateIconButton(
                I18N.Get("toolbar.search.clear"),
                "dismiss.png",
                ClearSearch,
                "ee4v-asset-manager-control-search__clear");

            hierarchy.Add(_optionsButton);
            hierarchy.Add(inputHost);
            hierarchy.Add(_clearButton);
            RefreshState();
        }

        public string value
        {
            get { return _field.value ?? string.Empty; }
            set { _field.value = value ?? string.Empty; }
        }

        public VisualElement SearchOptionsAnchor => _optionsButton;

        public event Action SearchOptionsClicked;

        public void RegisterValueChangedCallback(
            EventCallback<ChangeEvent<string>> callback)
        {
            _field.RegisterValueChangedCallback(callback);
        }

        private void ClearSearch()
        {
            if (!string.IsNullOrEmpty(value))
            {
                value = string.Empty;
            }
        }

        private void RefreshState()
        {
            var hasValue = !string.IsNullOrWhiteSpace(value);
            EnableInClassList(
                "ee4v-asset-manager-control-search--focused",
                _isFocused);
            _clearButton.style.display = hasValue
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            _placeholder.style.display = !hasValue && !_isFocused
                ? DisplayStyle.Flex
                : DisplayStyle.None;
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

        private AssetManagerButton CreateEndpointButton(
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
        private readonly AssetManagerButton _header;
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
            _header.SetText(_text);
            _header.SetIcon(
                AssetManagerControls.LoadFluentIconTexture(
                    expanded
                        ? "chevron_down.png"
                        : "chevron_right.png"),
                UiSizeTokens.Size12);
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

    internal sealed class AssetManagerActionRow : VisualElement
    {
        public AssetManagerActionRow()
        {
            AddToClassList("ee4v-asset-manager__actions");
        }
    }

}
