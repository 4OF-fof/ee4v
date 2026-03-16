using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class UiCardState
    {
        public UiCardState(string title, string description = "", string eyebrow = "")
        {
            Title = title ?? string.Empty;
            Description = description ?? string.Empty;
            Eyebrow = eyebrow ?? string.Empty;
        }

        public string Title { get; }

        public string Description { get; }

        public string Eyebrow { get; }
    }

    internal sealed class UiCard : VisualElement
    {
        private readonly Label _eyebrowLabel;
        private readonly Label _titleLabel;
        private readonly Label _descriptionLabel;

        public UiCard(UiCardState state = null)
        {
            AddToClassList(UiClassNames.Card);

            var header = new VisualElement();
            header.AddToClassList(UiClassNames.CardHeader);

            var headerText = new VisualElement();
            headerText.style.flexGrow = 1f;

            _eyebrowLabel = new Label();
            _eyebrowLabel.AddToClassList(UiClassNames.CardEyebrow);

            _titleLabel = new Label();
            _titleLabel.AddToClassList(UiClassNames.CardTitle);

            _descriptionLabel = new Label();
            _descriptionLabel.AddToClassList(UiClassNames.CardDescription);

            headerText.Add(_eyebrowLabel);
            headerText.Add(_titleLabel);
            headerText.Add(_descriptionLabel);

            HeaderRight = new VisualElement();
            HeaderRight.AddToClassList(UiClassNames.CardHeaderRight);

            header.Add(headerText);
            header.Add(HeaderRight);

            Body = new VisualElement();
            Body.AddToClassList(UiClassNames.CardBody);

            Add(header);
            Add(Body);

            SetState(state ?? new UiCardState(string.Empty));
        }

        public VisualElement HeaderRight { get; }

        public VisualElement Body { get; }

        public void SetState(UiCardState state)
        {
            state = state ?? new UiCardState(string.Empty);

            _eyebrowLabel.text = state.Eyebrow;
            _eyebrowLabel.style.display = string.IsNullOrWhiteSpace(state.Eyebrow) ? DisplayStyle.None : DisplayStyle.Flex;

            _titleLabel.text = state.Title;
            _titleLabel.style.display = string.IsNullOrWhiteSpace(state.Title) ? DisplayStyle.None : DisplayStyle.Flex;

            _descriptionLabel.text = state.Description;
            _descriptionLabel.style.display = string.IsNullOrWhiteSpace(state.Description) ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }
}
