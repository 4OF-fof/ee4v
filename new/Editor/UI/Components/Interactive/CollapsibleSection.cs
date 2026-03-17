using System;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class CollapsibleSectionState
    {
        public CollapsibleSectionState(string title, string meta = null, bool expanded = false, bool enabled = true)
        {
            Title = title ?? string.Empty;
            Meta = meta ?? string.Empty;
            Expanded = expanded;
            Enabled = enabled;
        }

        public string Title { get; }

        public string Meta { get; }

        public bool Expanded { get; }

        public bool Enabled { get; }
    }

    internal sealed class CollapsibleSection : VisualElement
    {
        private readonly Button _headerButton;
        private readonly UiTextElement _chevronLabel;
        private readonly UiTextElement _titleLabel;
        private readonly UiTextElement _metaLabel;

        public CollapsibleSection(CollapsibleSectionState state = null)
        {
            AddToClassList(UiClassNames.CollapsibleSection);

            _headerButton = new Button(ToggleExpanded);
            _headerButton.AddToClassList(UiClassNames.CollapsibleSectionHeader);

            _chevronLabel = UiTextFactory.Create(">", UiClassNames.CollapsibleSectionChevron);

            var headerText = new VisualElement();
            headerText.AddToClassList(UiClassNames.CollapsibleSectionHeaderText);

            _titleLabel = UiTextFactory.Create(string.Empty, UiClassNames.CollapsibleSectionTitle);
            _metaLabel = UiTextFactory.Create(string.Empty, UiClassNames.CollapsibleSectionMeta);

            headerText.Add(_titleLabel);
            headerText.Add(_metaLabel);

            _headerButton.Add(_chevronLabel);
            _headerButton.Add(headerText);

            Content = new VisualElement();
            Content.AddToClassList(UiClassNames.CollapsibleSectionContent);

            Add(_headerButton);
            Add(Content);

            SetState(state ?? new CollapsibleSectionState(string.Empty));
        }

        public event Action<bool> ExpandedChanged;

        public VisualElement Content { get; }

        public bool Expanded { get; private set; }

        public void SetState(CollapsibleSectionState state)
        {
            state = state ?? new CollapsibleSectionState(string.Empty);
            _titleLabel.SetText(state.Title);
            _metaLabel.SetText(state.Meta);
            _metaLabel.style.display = string.IsNullOrWhiteSpace(state.Meta) ? DisplayStyle.None : DisplayStyle.Flex;
            _headerButton.SetEnabled(state.Enabled);
            SetExpanded(state.Expanded, notify: false);
        }

        public void SetExpanded(bool expanded, bool notify = true)
        {
            Expanded = expanded;
            EnableInClassList(UiClassNames.CollapsibleSectionExpanded, expanded);
            _chevronLabel.SetText(expanded ? "v" : ">");
            Content.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;

            if (notify)
            {
                ExpandedChanged?.Invoke(expanded);
            }
        }

        private void ToggleExpanded()
        {
            SetExpanded(!Expanded);
        }
    }
}
