using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal enum UiButtonVariant
    {
        Solid,
        Ghost
    }

    internal sealed class UiButton : Button
    {
        private const string RootClassName = "ee4v-ui-button";
        private const string SolidClassName = "ee4v-ui-button--solid";
        private const string GhostClassName = "ee4v-ui-button--ghost";
        private const string CompactClassName = "ee4v-ui-button--compact";
        private const string IconOnlyClassName = "ee4v-ui-button--icon-only";
        private const string ContentClassName = "ee4v-ui-button__content";
        private const string ContentWithIconClassName =
            "ee4v-ui-button__content--with-icon";
        private const string IconClassName = "ee4v-ui-button__icon";
        private const string LabelClassName = "ee4v-ui-button__label";

        private readonly VisualElement _content;
        private readonly Icon _icon;
        private readonly UiTextElement _label;
        private bool _hasIcon;

        public UiButton(
            string label,
            Action onClick = null,
            string tooltip = null,
            IconState icon = null,
            UiButtonVariant variant = UiButtonVariant.Solid,
            bool compact = false,
            string labelTypographyClassName = null)
            : base(onClick)
        {
            AddToClassList(RootClassName);
            EnableInClassList(
                SolidClassName,
                variant == UiButtonVariant.Solid);
            EnableInClassList(
                GhostClassName,
                variant == UiButtonVariant.Ghost);
            EnableInClassList(CompactClassName, compact);
            this.tooltip = tooltip ?? string.Empty;

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
                string.IsNullOrWhiteSpace(labelTypographyClassName)
                    ? UiClassNames.ButtonLabel
                    : labelTypographyClassName,
                LabelClassName);
            _label.pickingMode = PickingMode.Ignore;
            _label.SetWhiteSpace(WhiteSpace.NoWrap);
            _content.Add(_icon);
            _content.Add(_label);
            Add(_content);

            SetIcon(icon);
            SetLabel(label);
        }

        public void SetLabel(string label)
        {
            _label.SetText(label);
            RefreshLayout();
        }

        public void SetLabelColor(Color color)
        {
            _label.SetColor(color);
        }

        public void SetLabelTextAlign(TextAnchor alignment)
        {
            _label.SetTextAlign(alignment);
        }

        public void SetIcon(IconState icon)
        {
            _hasIcon = icon != null;
            if (_hasIcon)
            {
                _icon.SetState(icon);
            }
            else
            {
                _icon.style.display = DisplayStyle.None;
            }

            RefreshLayout();
        }

        private void RefreshLayout()
        {
            var hasLabel = !string.IsNullOrWhiteSpace(_label.Text);
            _content.EnableInClassList(
                ContentWithIconClassName,
                _hasIcon && hasLabel);
            EnableInClassList(
                IconOnlyClassName,
                _hasIcon && !hasLabel);
            _label.style.display = hasLabel
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }
    }
}
