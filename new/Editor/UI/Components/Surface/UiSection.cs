using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class UiSectionState
    {
        public UiSectionState(string title, string description = "", string badgeText = "")
        {
            Title = title ?? string.Empty;
            Description = description ?? string.Empty;
            BadgeText = badgeText ?? string.Empty;
        }

        public string Title { get; }

        public string Description { get; }

        public string BadgeText { get; }
    }

    internal sealed class UiSection : VisualElement
    {
        private readonly Label _titleLabel;
        private readonly Label _descriptionLabel;
        private readonly Label _badgeLabel;

        public UiSection(UiSectionState state = null)
        {
            AddToClassList(UiClassNames.Section);

            var header = new VisualElement();
            header.AddToClassList(UiClassNames.SectionHeader);

            var titleStack = new VisualElement();
            titleStack.style.flexGrow = 1f;

            _titleLabel = new Label();
            _titleLabel.AddToClassList(UiClassNames.SectionTitle);

            _descriptionLabel = new Label();
            _descriptionLabel.AddToClassList(UiClassNames.SectionDescription);

            _badgeLabel = new Label();
            _badgeLabel.AddToClassList(UiClassNames.SectionBadge);

            titleStack.Add(_titleLabel);
            titleStack.Add(_descriptionLabel);
            header.Add(titleStack);
            header.Add(_badgeLabel);

            Body = new VisualElement();
            Body.AddToClassList(UiClassNames.SectionBody);

            Add(header);
            Add(Body);

            SetState(state ?? new UiSectionState(string.Empty));
        }

        public VisualElement Body { get; }

        public void SetState(UiSectionState state)
        {
            state = state ?? new UiSectionState(string.Empty);

            _titleLabel.text = state.Title;
            _titleLabel.style.display = string.IsNullOrWhiteSpace(state.Title) ? DisplayStyle.None : DisplayStyle.Flex;

            _descriptionLabel.text = state.Description;
            _descriptionLabel.style.display = string.IsNullOrWhiteSpace(state.Description) ? DisplayStyle.None : DisplayStyle.Flex;

            _badgeLabel.text = state.BadgeText;
            _badgeLabel.style.display = string.IsNullOrWhiteSpace(state.BadgeText) ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }
}
