using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    public enum SelectionTabVariant
    {
        Default,
        Primary,
        Underline
    }

    public sealed class SelectionTab : VisualElement
    {
        private readonly UiTextElement _label;
        private readonly SelectionTabVariant _variant;

        public static VisualElement CreateStrip()
        {
            var strip = new VisualElement();
            strip.AddToClassList("ee4v-ui-selection-tabs");
            return strip;
        }

        public SelectionTab(string title, SelectionTabVariant variant = SelectionTabVariant.Default,
            Action onClick = null)
        {
            _variant = variant;
            focusable = true;
            AddToClassList("ee4v-ui-selection-tab");
            AddToClassList(variant == SelectionTabVariant.Primary
                ? "ee4v-ui-selection-tab--primary"
                : "ee4v-ui-selection-tab--default");
            EnableInClassList("ee4v-ui-selection-tab--underline", variant == SelectionTabVariant.Underline);
            _label = UiTextFactory.Create(title,
                "ee4v-ui-selection-tab-name");
            _label.SetFontSize(variant == SelectionTabVariant.Underline
                ? UiTypographyTokens.BodyFontSize : UiTypographyTokens.SubtitleFontSize);
            _label.SetTextAlign(variant == SelectionTabVariant.Underline
                ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft);
            _label.tooltip = title;
            _label.pickingMode = PickingMode.Ignore;
            Add(_label);
            if (onClick != null) { RegisterCallback<ClickEvent>(_ => onClick()); }
            RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.target != this || !enabledInHierarchy ||
                    (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter && evt.keyCode != KeyCode.Space))
                {
                    return;
                }
                using (var click = ClickEvent.GetPooled())
                {
                    click.target = this;
                    SendEvent(click);
                }
                evt.PreventDefault();
                evt.StopPropagation();
            });
            SetSelected(false);
        }

        public void SetSelected(bool selected)
        {
            EnableInClassList("ee4v-ui-selection-tab--selected", selected);
            if (_variant == SelectionTabVariant.Underline)
            {
                _label.SetColor(selected ? UiColorTokens.TextPrimary : UiColorTokens.TextSecondary);
            }
        }
    }
}
