using System;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    public sealed class DisclosureSection : VisualElement
    {
        private readonly UiButton _header;
        private bool _expanded;

        public DisclosureSection(
            string title = null,
            bool expanded = true)
        {
            AddToClassList("ee4v-ui-disclosure-section");
            _header = new UiButton(
                string.Empty,
                Toggle,
                variant: UiButtonVariant.Ghost,
                labelTypographyClassName: UiClassNames.SectionTitle);
            _header.AddToClassList(
                "ee4v-ui-disclosure-section__header");
            Body = new VisualElement();
            Body.AddToClassList("ee4v-ui-disclosure-section__body");
            hierarchy.Add(_header);
            hierarchy.Add(Body);
            SetTitle(title);
            SetExpanded(expanded, false);
        }

        public Button Header => _header;
        public VisualElement Body { get; }
        public bool Expanded => _expanded;
        public override VisualElement contentContainer => Body;

        public event Action<bool> ExpandedChanged;

        public void SetTitle(string title)
        {
            _header.SetLabel(title ?? string.Empty);
        }

        public void SetExpanded(bool expanded)
        {
            SetExpanded(expanded, true);
        }

        private void Toggle()
        {
            SetExpanded(!_expanded, true);
        }

        private void SetExpanded(bool expanded, bool notify)
        {
            if (_expanded == expanded &&
                Body.style.display.value ==
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
            Body.style.display = expanded
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            EnableInClassList(
                "ee4v-ui-disclosure-section--expanded",
                expanded);
            if (notify)
            {
                ExpandedChanged?.Invoke(expanded);
            }
        }
    }
}
