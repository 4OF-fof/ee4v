using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    public sealed class TagPillState
    {
        public TagPillState(
            string text,
            string removeTooltip = null,
            IconState icon = null)
        {
            Text = text ?? string.Empty;
            RemoveTooltip = removeTooltip ?? string.Empty;
            Icon = icon;
        }

        public string Text { get; }
        public string RemoveTooltip { get; }
        public IconState Icon { get; }
    }

    public sealed class TagPill : VisualElement
    {
        private readonly Icon _icon;
        private readonly UiTextElement _text;
        private readonly UiButton _removeButton;

        public TagPill(
            TagPillState state = null,
            Action onRemove = null,
            Action onClick = null)
        {
            AddToClassList("ee4v-ui-tag-pill");
            EnableInClassList(
                "ee4v-ui-tag-pill--clickable",
                onClick != null);
            EnableInClassList(
                "ee4v-ui-tag-pill--without-remove",
                onRemove == null);

            _icon = new Icon
            {
                pickingMode = PickingMode.Ignore
            };
            _icon.AddToClassList("ee4v-ui-tag-pill__icon");
            Add(_icon);

            _text = UiTextFactory.Create(
                string.Empty,
                "ee4v-ui-tag-pill__text");
            _text.SetWhiteSpace(WhiteSpace.NoWrap);
            _text.pickingMode = PickingMode.Ignore;
            Add(_text);

            _removeButton = new UiButton(
                string.Empty,
                onRemove,
                icon: FluentUiIcons.CreateState(
                    "dismiss.png",
                    UiSizeTokens.Size12),
                variant: UiButtonVariant.Ghost,
                compact: true);
            _removeButton.AddToClassList(
                "ee4v-ui-tag-pill__remove");
            _removeButton.RegisterCallback<ClickEvent>(
                evt => evt.StopPropagation());
            _removeButton.style.display = onRemove == null
                ? DisplayStyle.None
                : DisplayStyle.Flex;
            Add(_removeButton);

            if (onClick != null)
            {
                focusable = true;
                RegisterCallback<ClickEvent>(_ => onClick());
                RegisterCallback<KeyDownEvent>(evt =>
                {
                    if (evt.keyCode != KeyCode.Return &&
                        evt.keyCode != KeyCode.KeypadEnter &&
                        evt.keyCode != KeyCode.Space)
                    {
                        return;
                    }

                    evt.StopPropagation();
                    onClick();
                });
            }

            SetState(state ?? new TagPillState(string.Empty));
        }

        public void SetState(TagPillState state)
        {
            state = state ?? new TagPillState(string.Empty);
            if (state.Icon == null)
            {
                _icon.style.display = DisplayStyle.None;
            }
            else
            {
                _icon.SetState(state.Icon);
                _icon.style.display = DisplayStyle.Flex;
            }
            _text.SetText(state.Text);
            _removeButton.tooltip = state.RemoveTooltip;
        }
    }
}
