using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class InfoCardState
    {
        public InfoCardState(string title, string description = null, string eyebrow = null, string badgeText = null)
            : this(
                title,
                description,
                eyebrow,
                string.IsNullOrWhiteSpace(badgeText)
                    ? null
                    : new StatusBadgeState(badgeText, UiStatusTone.Idle))
        {
        }

        public InfoCardState(string title, string description, string eyebrow, StatusBadgeState badgeState)
        {
            Title = title ?? string.Empty;
            Description = description ?? string.Empty;
            Eyebrow = eyebrow ?? string.Empty;
            BadgeState = badgeState;
        }

        public string Title { get; }

        public string Description { get; }

        public string Eyebrow { get; }

        public StatusBadgeState BadgeState { get; }
    }

    internal class InfoCard : VisualElement
    {
        private const string RootClassName = "ee4v-ui-info-card";
        private const string HeaderClassName = "ee4v-ui-info-card__header";
        private const string HeaderRightClassName = "ee4v-ui-info-card__header-right";
        private const string BodyClassName = "ee4v-ui-info-card__body";
        private const string TitleWithDescriptionClassName = "ee4v-ui-info-card__title--with-description";
        private const string HeaderSingleLineClassName = "ee4v-ui-info-card__header--single-line";
        private const string BodyWithHeaderClassName = "ee4v-ui-info-card__body--with-header";
        private const string BodyCompactClassName = "ee4v-ui-info-card__body--compact";
        private readonly VisualElement _header;
        private readonly VisualElement _headerText;
        private readonly UiTextElement _eyebrowLabel;
        private readonly UiTextElement _titleLabel;
        private readonly UiTextElement _descriptionLabel;
        private readonly StatusBadge _badge;
        private InfoCardState _state;

        public InfoCard(InfoCardState state = null)
        {
            AddToClassList(RootClassName);

            _header = new VisualElement();
            _header.AddToClassList(HeaderClassName);

            _headerText = new VisualElement();
            _headerText.style.flexGrow = 1f;
            _headerText.style.flexShrink = 1f;
            _headerText.style.minWidth = 0f;
            _headerText.AddToClassList("ee4v-ui-info-card__header-text");

            _eyebrowLabel = UiTextFactory.Create(string.Empty, UiClassNames.InfoCardEyebrow);
            _titleLabel = UiTextFactory.Create(string.Empty, UiClassNames.InfoCardTitle);
            _descriptionLabel = UiTextFactory.Create(string.Empty, UiClassNames.InfoCardDescription);
            _headerText.Add(_eyebrowLabel);
            _headerText.Add(_titleLabel);
            _headerText.Add(_descriptionLabel);

            HeaderRight = new VisualElement();
            HeaderRight.AddToClassList(HeaderRightClassName);
            _badge = new StatusBadge();
            HeaderRight.Add(_badge);

            _header.Add(_headerText);
            _header.Add(HeaderRight);

            Body = new InfoCardBodyElement(RefreshLayout);
            Body.AddToClassList(BodyClassName);

            Add(_header);
            Add(Body);

            SetState(state ?? new InfoCardState(string.Empty));
        }

        public VisualElement HeaderRight { get; }

        public InfoCardBodyElement Body { get; }

        public StatusBadge Badge
        {
            get { return _badge; }
        }

        public void SetState(InfoCardState state)
        {
            _state = state ?? new InfoCardState(string.Empty);

            var hasEyebrow = !string.IsNullOrWhiteSpace(_state.Eyebrow);
            var hasTitle = !string.IsNullOrWhiteSpace(_state.Title);
            var hasDescription = !string.IsNullOrWhiteSpace(_state.Description);
            var hasBadge = _state.BadgeState != null && !string.IsNullOrWhiteSpace(_state.BadgeState.Text);
            var isSingleLineHeader = hasTitle && !hasEyebrow && !hasDescription;

            _eyebrowLabel.SetText(_state.Eyebrow);
            _eyebrowLabel.style.display = hasEyebrow ? DisplayStyle.Flex : DisplayStyle.None;

            _titleLabel.SetText(_state.Title);
            _titleLabel.style.display = hasTitle ? DisplayStyle.Flex : DisplayStyle.None;
            _titleLabel.EnableInClassList(TitleWithDescriptionClassName, hasDescription);

            _descriptionLabel.SetText(_state.Description);
            _descriptionLabel.style.display = hasDescription ? DisplayStyle.Flex : DisplayStyle.None;

            _badge.SetState(_state.BadgeState ?? new StatusBadgeState(string.Empty, UiStatusTone.Idle));

            var hasHeaderText = hasEyebrow || hasTitle || hasDescription;
            var hasHeaderRight = hasBadge || HasVisibleHeaderRightChild();
            HeaderRight.style.display = hasHeaderRight ? DisplayStyle.Flex : DisplayStyle.None;
            _header.style.display = hasHeaderText || hasHeaderRight ? DisplayStyle.Flex : DisplayStyle.None;
            _header.EnableInClassList(HeaderSingleLineClassName, isSingleLineHeader);

            RefreshLayout();
        }

        public void RefreshLayout()
        {
            var hasBody = HasVisibleBodyChild();
            var hasHeader = _header.style.display != DisplayStyle.None;
            var hasDescription = _state != null && !string.IsNullOrWhiteSpace(_state.Description);

            Body.style.display = hasBody ? DisplayStyle.Flex : DisplayStyle.None;
            Body.EnableInClassList(BodyWithHeaderClassName, hasBody && hasHeader && hasDescription);
            Body.EnableInClassList(BodyCompactClassName, hasBody && hasHeader && !hasDescription);
        }

        private bool HasVisibleHeaderRightChild()
        {
            for (var i = 0; i < HeaderRight.childCount; i++)
            {
                var child = HeaderRight.ElementAt(i);
                if (ReferenceEquals(child, _badge))
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
