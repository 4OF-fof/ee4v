using System;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal enum UiButtonVariant
    {
        Solid,
        Ghost
    }

    internal enum UiButtonSize
    {
        Default,
        Compact
    }

    internal sealed class UiButtonState
    {
        public UiButtonState(
            string label = null,
            string meta = null,
            string tooltip = null,
            IconState iconState = null,
            bool enabled = true,
            bool selected = false,
            UiButtonVariant variant = UiButtonVariant.Solid,
            UiButtonSize size = UiButtonSize.Default)
        {
            Label = label ?? string.Empty;
            Meta = meta ?? string.Empty;
            Tooltip = tooltip ?? string.Empty;
            IconState = iconState;
            Enabled = enabled;
            Selected = selected;
            Variant = variant;
            Size = size;
        }

        public string Label { get; }

        public string Meta { get; }

        public string Tooltip { get; }

        public IconState IconState { get; }

        public bool Enabled { get; }

        public bool Selected { get; }

        public UiButtonVariant Variant { get; }

        public UiButtonSize Size { get; }
    }

    internal sealed class UiButton : Button
    {
        private const string RootClassName = "ee4v-ui-button";
        private const string SolidClassName = "ee4v-ui-button--solid";
        private const string GhostClassName = "ee4v-ui-button--ghost";
        private const string CompactClassName = "ee4v-ui-button--compact";
        private const string SelectedClassName = "ee4v-ui-button--selected";
        private const string IconOnlyClassName = "ee4v-ui-button--icon-only";
        private const string ContentClassName = "ee4v-ui-button__content";
        private const string ContentWithIconClassName =
            "ee4v-ui-button__content--with-icon";
        private const string IconClassName = "ee4v-ui-button__icon";
        private const string LabelClassName = "ee4v-ui-button__label";
        private const string MetaClassName = "ee4v-ui-button__meta";

        private readonly VisualElement _content;
        private readonly Icon _icon;
        private readonly UiTextElement _label;
        private readonly UiTextElement _meta;
        private bool _enabled;
        private bool _selected;

        public UiButton(Action onClick = null)
            : this(
                new UiButtonState(),
                UiClassNames.ButtonLabel,
                onClick)
        {
        }

        public UiButton(
            UiButtonState state,
            Action onClick = null)
            : this(
                state,
                UiClassNames.ButtonLabel,
                onClick)
        {
        }

        public UiButton(
            UiButtonState state,
            string labelTypographyClassName,
            Action onClick = null)
            : base(onClick)
        {
            text = string.Empty;
            AddToClassList(RootClassName);

            _content = new VisualElement
            {
                pickingMode = PickingMode.Ignore
            };
            _content.AddToClassList(ContentClassName);

            _icon = new Icon
            {
                pickingMode = PickingMode.Ignore
            };
            _icon.AddToClassList(IconClassName);

            _label = UiTextFactory.Create(
                string.Empty,
                string.IsNullOrWhiteSpace(
                    labelTypographyClassName)
                    ? UiClassNames.ButtonLabel
                    : labelTypographyClassName,
                LabelClassName);
            _label.pickingMode = PickingMode.Ignore;
            _label.SetWhiteSpace(WhiteSpace.NoWrap);

            _meta = UiTextFactory.Create(
                string.Empty,
                UiClassNames.ButtonMeta,
                MetaClassName);
            _meta.pickingMode = PickingMode.Ignore;
            _meta.SetWhiteSpace(WhiteSpace.NoWrap);

            _content.Add(_icon);
            _content.Add(_label);
            _content.Add(_meta);
            Add(_content);

            SetState(state);
        }

        public UiTextElement LabelElement
        {
            get { return _label; }
        }

        public UiTextElement MetaElement
        {
            get { return _meta; }
        }

        public Icon IconElement
        {
            get { return _icon; }
        }

        public bool Selected
        {
            get { return _selected; }
        }

        public void SetState(UiButtonState state)
        {
            state = state ?? new UiButtonState();
            tooltip = state.Tooltip;
            _enabled = state.Enabled;
            _selected = state.Selected;

            _label.SetText(state.Label);
            _meta.SetText(state.Meta);

            var hasIcon = state.IconState != null;
            if (hasIcon)
            {
                _icon.SetState(state.IconState);
            }
            else
            {
                _icon.style.display = DisplayStyle.None;
            }

            var hasText =
                !string.IsNullOrWhiteSpace(state.Label) ||
                !string.IsNullOrWhiteSpace(state.Meta);
            _content.EnableInClassList(
                ContentWithIconClassName,
                hasIcon && hasText);
            EnableInClassList(
                IconOnlyClassName,
                hasIcon && !hasText);
            EnableInClassList(
                SolidClassName,
                state.Variant == UiButtonVariant.Solid);
            EnableInClassList(
                GhostClassName,
                state.Variant == UiButtonVariant.Ghost);
            EnableInClassList(
                CompactClassName,
                state.Size == UiButtonSize.Compact);
            EnableInClassList(SelectedClassName, _selected);
            SetInteractable(_enabled);
        }

        public void SetSelected(bool selected)
        {
            _selected = selected;
            EnableInClassList(SelectedClassName, selected);
            RefreshTextColors();
        }

        public void SetInteractable(bool enabled)
        {
            _enabled = enabled;
            SetEnabled(enabled);
            RefreshTextColors();
        }

        private void RefreshTextColors()
        {
            var labelColor = !_enabled
                ? UiColorTokens.TextDisabled
                : _selected
                    ? UiColorTokens.TextOnState
                    : UiColorTokens.TextPrimary;
            var metaColor = !_enabled
                ? UiColorTokens.TextDisabled
                : _selected
                    ? UiColorTokens.TextOnState
                    : UiColorTokens.TextMuted;
            _label.SetColor(labelColor);
            _meta.SetColor(metaColor);
        }
    }
}
