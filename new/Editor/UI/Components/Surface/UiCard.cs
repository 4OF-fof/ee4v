using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class UiCardState
    {
        public UiCardState(string title = null, string description = null, string eyebrow = null, string badgeText = null)
        {
            Title = title ?? string.Empty;
            Description = description ?? string.Empty;
            Eyebrow = eyebrow ?? string.Empty;
            BadgeText = badgeText ?? string.Empty;
        }

        public string Title { get; }

        public string Description { get; }

        public string Eyebrow { get; }

        public string BadgeText { get; }
    }

    internal sealed class UiCard : VisualElement
    {
        private readonly VisualElement _header;
        private readonly Label _eyebrowLabel;
        private readonly Label _titleLabel;
        private readonly Label _descriptionLabel;
        private readonly Label _badgeLabel;

        public UiCard(UiCardState state = null)
        {
            AddToClassList(UiClassNames.Card);

            _header = new VisualElement();
            _header.AddToClassList(UiClassNames.CardHeader);

            var headerText = new VisualElement();
            headerText.style.flexGrow = 1f;

            _eyebrowLabel = new Label();
            _eyebrowLabel.AddToClassList(UiClassNames.CardEyebrow);

            _titleLabel = new Label();
            _titleLabel.AddToClassList(UiClassNames.CardTitle);

            _descriptionLabel = new Label();
            _descriptionLabel.AddToClassList(UiClassNames.CardDescription);

            _badgeLabel = new Label();
            _badgeLabel.AddToClassList(UiClassNames.CardBadge);

            headerText.Add(_eyebrowLabel);
            headerText.Add(_titleLabel);
            headerText.Add(_descriptionLabel);

            HeaderRight = new VisualElement();
            HeaderRight.AddToClassList(UiClassNames.CardHeaderRight);
            HeaderRight.Add(_badgeLabel);

            _header.Add(headerText);
            _header.Add(HeaderRight);

            Body = new VisualElement();
            Body.AddToClassList(UiClassNames.CardBody);

            Add(_header);
            Add(Body);

            SetState(state ?? new UiCardState());
        }

        public VisualElement HeaderRight { get; }

        public VisualElement Body { get; }

        public void SetState(UiCardState state)
        {
            state = state ?? new UiCardState();

            _eyebrowLabel.text = state.Eyebrow;
            _eyebrowLabel.style.display = string.IsNullOrWhiteSpace(state.Eyebrow) ? DisplayStyle.None : DisplayStyle.Flex;

            _titleLabel.text = state.Title;
            _titleLabel.style.display = string.IsNullOrWhiteSpace(state.Title) ? DisplayStyle.None : DisplayStyle.Flex;

            _descriptionLabel.text = state.Description;
            _descriptionLabel.style.display = string.IsNullOrWhiteSpace(state.Description) ? DisplayStyle.None : DisplayStyle.Flex;

            _badgeLabel.text = state.BadgeText;
            _badgeLabel.style.display = string.IsNullOrWhiteSpace(state.BadgeText) ? DisplayStyle.None : DisplayStyle.Flex;

            var hasHeaderText = !string.IsNullOrWhiteSpace(state.Eyebrow)
                || !string.IsNullOrWhiteSpace(state.Title)
                || !string.IsNullOrWhiteSpace(state.Description);
            var hasHeaderRight = !string.IsNullOrWhiteSpace(state.BadgeText) || HeaderRight.childCount > 1;
            _header.style.display = hasHeaderText || hasHeaderRight ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
