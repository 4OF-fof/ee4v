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
        private readonly UiTextElement _eyebrowLabel;
        private readonly UiTextElement _titleLabel;
        private readonly UiTextElement _descriptionLabel;
        private readonly UiTextElement _badgeLabel;

        public UiCard(UiCardState state = null)
        {
            AddToClassList(UiClassNames.Card);

            _header = new VisualElement();
            _header.AddToClassList(UiClassNames.CardHeader);

            var headerText = new VisualElement();
            headerText.style.flexGrow = 1f;

            _eyebrowLabel = UiTextFactory.Create(string.Empty, UiClassNames.CardEyebrow);

            _titleLabel = UiTextFactory.Create(string.Empty, UiClassNames.CardTitle);

            _descriptionLabel = UiTextFactory.Create(string.Empty, UiClassNames.CardDescription);

            _badgeLabel = UiTextFactory.Create(string.Empty, UiClassNames.CardBadge);

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

            _eyebrowLabel.SetText(state.Eyebrow);

            _titleLabel.SetText(state.Title);

            _descriptionLabel.SetText(state.Description);

            _badgeLabel.SetText(state.BadgeText);

            var hasHeaderText = !string.IsNullOrWhiteSpace(state.Eyebrow)
                || !string.IsNullOrWhiteSpace(state.Title)
                || !string.IsNullOrWhiteSpace(state.Description);
            var hasHeaderRight = !string.IsNullOrWhiteSpace(state.BadgeText) || HasVisibleHeaderRightChild();
            _header.style.display = hasHeaderText || hasHeaderRight ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private bool HasVisibleHeaderRightChild()
        {
            for (var i = 0; i < HeaderRight.childCount; i++)
            {
                var child = HeaderRight.ElementAt(i);
                if (ReferenceEquals(child, _badgeLabel))
                {
                    continue;
                }

                if (child.style.display != DisplayStyle.None)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
