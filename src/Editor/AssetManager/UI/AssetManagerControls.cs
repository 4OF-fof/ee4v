using System;
using Ee4v.Core.EditorIntegration;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal static class AssetManagerControls
    {
        private const string ReloadIconPath =
            "/Editor/ThirdParty/FluentUiSystemIcons/" +
            "Png512/arrow_clockwise.png";

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
            var packageRoot = PackageAssetApi.GetPackageRootAssetPath();
            var texture = string.IsNullOrEmpty(packageRoot)
                ? null
                : AssetDatabase.LoadAssetAtPath<Texture2D>(
                    packageRoot + ReloadIconPath);
            var button = new AssetManagerButton(
                texture == null ? "Reload" : string.Empty,
                onClick,
                classNames);
            button.tooltip = "Reload";
            if (texture != null)
            {
                button.AddToClassList(
                    "ee4v-asset-manager-control-button--icon-only");
                button.SetIcon(texture, UiSizeTokens.Size12);
            }

            return button;
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
            params string[] classNames)
        {
            return new AssetManagerEnumField(label, value, classNames);
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

        public AssetManagerButton(
            string text,
            Action onClick,
            params string[] classNames)
            : base(onClick)
        {
            base.text = string.Empty;
            AddToClassList("ee4v-asset-manager-control-button");
            AssetManagerControls.AddClasses(this, classNames);

            _label = UiTextFactory.Create(
                text,
                UiClassNames.ButtonLabel,
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

        public void SetIcon(Texture texture, float size)
        {
            if (texture == null)
            {
                return;
            }

            var icon = new Image
            {
                image = texture,
                tintColor = UiColorTokens.TextPrimary,
                scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore
            };
            icon.AddToClassList(
                "ee4v-asset-manager-control-button__icon");
            icon.style.width = size;
            icon.style.height = size;
            hierarchy.Insert(0, icon);
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
    }

    internal sealed class AssetManagerSearchField : VisualElement
    {
        private readonly TextField _field;
        private readonly UiTextElement _placeholder;
        private readonly AssetManagerButton _clearButton;
        private bool _isFocused;

        public AssetManagerSearchField(
            string placeholder,
            params string[] classNames)
        {
            AddToClassList("ee4v-asset-manager-control-search");
            AssetManagerControls.AddClasses(this, classNames);

            var searchIcon = new Icon(IconState.FromBuiltinIcon(
                UiBuiltinIcon.Search,
                size: UiSizeTokens.Size14));
            searchIcon.AddToClassList(
                "ee4v-asset-manager-control-search__icon");

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

            _clearButton = AssetManagerControls.CreateButton(
                string.Empty,
                ClearSearch,
                "ee4v-asset-manager-control-search__clear");
            _clearButton.tooltip = "Clear search";
            _clearButton.Add(new Icon(IconState.FromBuiltinIcon(
                UiBuiltinIcon.Close,
                size: UiSizeTokens.Size10)));

            hierarchy.Add(searchIcon);
            hierarchy.Add(inputHost);
            hierarchy.Add(_clearButton);
            RefreshState();
        }

        public string value
        {
            get { return _field.value ?? string.Empty; }
            set { _field.value = value ?? string.Empty; }
        }

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

            hierarchy.Add(CreateEndpointButton("−", -1));
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
            hierarchy.Add(CreateEndpointButton("+", 1));
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

        private UiTextElement CreateEndpointButton(
            string text,
            int delta)
        {
            var button = UiTextFactory.Create(
                text,
                "ee4v-asset-manager-grid-slider__endpoint");
            button.focusable = true;
            button.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != (int)MouseButton.LeftMouse)
                {
                    return;
                }

                button.Focus();
                SetValue(_slider.value + delta);
                evt.StopPropagation();
            });
            button.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Return &&
                    evt.keyCode != KeyCode.Space)
                {
                    return;
                }

                SetValue(_slider.value + delta);
                evt.StopPropagation();
            });
            return button;
        }
    }

    internal sealed class AssetManagerEnumField : VisualElement
    {
        private readonly EnumField _field;

        public AssetManagerEnumField(
            string label,
            Enum value,
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
            _field = UiTextFactory.CreateEnumField(
                string.Empty,
                value,
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

        public Enum value
        {
            get { return _field.value; }
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
            _header.SetText((expanded ? "▾ " : "▸ ") + _text);
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
            Add(UiTextFactory.Create(
                "i",
                "ee4v-asset-manager-control-notice__icon"));
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

    internal sealed class AssetManagerCard : VisualElement
    {
        private readonly VisualElement _body;

        public AssetManagerCard(string title, string description)
        {
            AddToClassList("ee4v-asset-manager__source-card");
            hierarchy.Add(UiTextFactory.Create(
                title,
                UiClassNames.InfoCardTitle,
                "ee4v-asset-manager__source-title"));
            hierarchy.Add(UiTextFactory.Create(
                description,
                UiClassNames.InfoCardDescription,
                "ee4v-asset-manager__source-description"));
            _body = new VisualElement();
            _body.AddToClassList("ee4v-asset-manager__source-card-body");
            hierarchy.Add(_body);
        }

        public override VisualElement contentContainer => _body;
    }
}
