using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class InfoCardState
    {
        public InfoCardState(string title, string description = null, string eyebrow = null, string badgeText = null)
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

    internal sealed class InfoCard : VisualElement
    {
        private readonly VisualElement _header;
        private readonly VisualElement _headerText;
        private readonly UiTextElement _eyebrowLabel;
        private readonly UiTextElement _titleLabel;
        private readonly UiTextElement _descriptionLabel;
        private readonly UiTextElement _badgeLabel;
        private InfoCardState _state;

        public InfoCard(InfoCardState state = null)
        {
            AddToClassList(UiClassNames.InfoCard);

            _header = new VisualElement();
            _header.AddToClassList(UiClassNames.InfoCardHeader);

            _headerText = new VisualElement();
            _headerText.style.flexGrow = 1f;
            _headerText.style.flexShrink = 1f;
            _headerText.style.minWidth = 0f;
            _headerText.AddToClassList("ee4v-ui-info-card__header-text");

            _eyebrowLabel = UiTextFactory.Create(string.Empty, UiClassNames.InfoCardEyebrow);
            _titleLabel = UiTextFactory.Create(string.Empty, UiClassNames.InfoCardTitle);
            _descriptionLabel = UiTextFactory.Create(string.Empty, UiClassNames.InfoCardDescription);
            _badgeLabel = UiTextFactory.Create(string.Empty, UiClassNames.InfoCardBadge);

            _headerText.Add(_eyebrowLabel);
            _headerText.Add(_titleLabel);
            _headerText.Add(_descriptionLabel);

            HeaderRight = new VisualElement();
            HeaderRight.AddToClassList(UiClassNames.InfoCardHeaderRight);
            HeaderRight.Add(_badgeLabel);

            _header.Add(_headerText);
            _header.Add(HeaderRight);

            Body = new InfoCardBodyElement(RefreshLayout);
            Body.AddToClassList(UiClassNames.InfoCardBody);

            Add(_header);
            Add(Body);

            SetState(state ?? new InfoCardState(string.Empty));
        }

        public VisualElement HeaderRight { get; }

        public InfoCardBodyElement Body { get; }

        public void SetState(InfoCardState state)
        {
            _state = state ?? new InfoCardState(string.Empty);

            var hasEyebrow = !string.IsNullOrWhiteSpace(_state.Eyebrow);
            var hasTitle = !string.IsNullOrWhiteSpace(_state.Title);
            var hasDescription = !string.IsNullOrWhiteSpace(_state.Description);
            var hasBadge = !string.IsNullOrWhiteSpace(_state.BadgeText);
            var isSingleLineHeader = hasTitle && !hasEyebrow && !hasDescription;

            _eyebrowLabel.SetText(_state.Eyebrow);
            _eyebrowLabel.style.display = hasEyebrow ? DisplayStyle.Flex : DisplayStyle.None;

            _titleLabel.SetText(_state.Title);
            _titleLabel.style.display = hasTitle ? DisplayStyle.Flex : DisplayStyle.None;
            _titleLabel.EnableInClassList("ee4v-ui-info-card__title--with-description", hasDescription);

            _descriptionLabel.SetText(_state.Description);
            _descriptionLabel.style.display = hasDescription ? DisplayStyle.Flex : DisplayStyle.None;

            _badgeLabel.SetText(_state.BadgeText);
            _badgeLabel.style.display = hasBadge ? DisplayStyle.Flex : DisplayStyle.None;

            var hasHeaderText = hasEyebrow || hasTitle || hasDescription;
            var hasHeaderRight = hasBadge || HasVisibleHeaderRightChild();
            HeaderRight.style.display = hasHeaderRight ? DisplayStyle.Flex : DisplayStyle.None;
            _header.style.display = hasHeaderText || hasHeaderRight ? DisplayStyle.Flex : DisplayStyle.None;
            _header.EnableInClassList("ee4v-ui-info-card__header--single-line", isSingleLineHeader);

            RefreshLayout();
        }

        public void RefreshLayout()
        {
            var hasBody = HasVisibleBodyChild();
            var hasHeader = _header.style.display != DisplayStyle.None;
            var hasDescription = _state != null && !string.IsNullOrWhiteSpace(_state.Description);

            Body.style.display = hasBody ? DisplayStyle.Flex : DisplayStyle.None;
            Body.EnableInClassList("ee4v-ui-info-card__body--with-header", hasBody && hasHeader && hasDescription);
            Body.EnableInClassList("ee4v-ui-info-card__body--compact", hasBody && hasHeader && !hasDescription);
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

        private bool HasVisibleBodyChild()
        {
            for (var i = 0; i < Body.childCount; i++)
            {
                if (Body.ElementAt(i).style.display != DisplayStyle.None)
                {
                    return true;
                }
            }

            return false;
        }

        internal sealed class InfoCardBodyElement : VisualElement
        {
            private readonly System.Action _onChanged;

            public InfoCardBodyElement(System.Action onChanged)
            {
                _onChanged = onChanged;
            }

            public new void Add(VisualElement child)
            {
                base.Add(child);
                _onChanged();
            }

            public new void Insert(int index, VisualElement child)
            {
                base.Insert(index, child);
                _onChanged();
            }

            public new void Remove(VisualElement child)
            {
                base.Remove(child);
                _onChanged();
            }

            public new void Clear()
            {
                base.Clear();
                _onChanged();
            }
        }
    }
}
