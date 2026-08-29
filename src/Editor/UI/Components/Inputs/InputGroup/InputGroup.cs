using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    public sealed class InputGroup : VisualElement
    {
        private const string HoveredClassName =
            "ee4v-ui-input-group--hovered";
        private const string ExpandedClassName =
            "ee4v-ui-input-group--expanded";
        private const string CollapsedClassName =
            "ee4v-ui-input-group--collapsed";
        private readonly UiButton _header;
        private bool _expanded;

        public InputGroup(
            string label,
            FormInput input,
            params FormInput[] additionalInputs)
        {
            AddToClassList("ee4v-ui-input-group");
            _header = new UiButton(
                string.Empty,
                Toggle,
                variant: UiButtonVariant.Ghost,
                labelTypographyClassName: UiClassNames.FormLabel);
            _header.AddToClassList("ee4v-ui-input-group__header");
            LabelText = _header.LabelText;
            Content = new VisualElement();
            Content.AddToClassList("ee4v-ui-input-group__content");
            hierarchy.Add(_header);
            hierarchy.Add(Content);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerLeaveEvent>(_ =>
                RemoveFromClassList(HoveredClassName));
            SetLabel(label);
            AddInput(input);

            for (var index = 0;
                 index < (additionalInputs?.Length ?? 0);
                 index++)
            {
                AddInput(additionalInputs[index]);
            }

            SetExpanded(true, false);
        }

        public Button Header => _header;
        public UiTextElement LabelText { get; }
        public VisualElement Content { get; }
        public bool Expanded => _expanded;

        public override VisualElement contentContainer => Content;

        public event Action<bool> ExpandedChanged;

        public void AddInput(FormInput input)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            Add(input);
        }

        public void SetLabel(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                throw new ArgumentException(
                    "InputGroup requires a label.",
                    nameof(label));
            }

            _header.SetLabel(label);
        }

        public void SetExpanded(bool expanded)
        {
            SetExpanded(expanded, true);
        }

        private void Toggle()
        {
            SetExpanded(!_expanded, true);
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            var pointerPosition = (Vector2)evt.position;
            var bounds = worldBound;
            var overBorder = bounds.Contains(pointerPosition) &&
                             (pointerPosition.x <=
                                  bounds.xMin + resolvedStyle.borderLeftWidth ||
                              pointerPosition.x >=
                                  bounds.xMax - resolvedStyle.borderRightWidth ||
                              pointerPosition.y <=
                                  bounds.yMin + resolvedStyle.borderTopWidth ||
                              pointerPosition.y >=
                                  bounds.yMax - resolvedStyle.borderBottomWidth);
            EnableInClassList(
                HoveredClassName,
                overBorder || _header.worldBound.Contains(pointerPosition));
        }

        private void SetExpanded(bool expanded, bool notify)
        {
            if (_expanded == expanded &&
                Content.style.display.value ==
                (expanded ? DisplayStyle.Flex : DisplayStyle.None))
            {
                return;
            }

            _expanded = expanded;
            _header.SetIcon(IconState.FromBuiltinIcon(
                expanded
                    ? UiBuiltinIcon.DisclosureOpen
                    : UiBuiltinIcon.DisclosureClosed,
                UiSizeTokens.Size12));
            Content.style.display = expanded
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            EnableInClassList(ExpandedClassName, expanded);
            EnableInClassList(CollapsedClassName, !expanded);
            if (notify)
            {
                ExpandedChanged?.Invoke(expanded);
            }
        }
    }
}
