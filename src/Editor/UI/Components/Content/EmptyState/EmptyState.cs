using UnityEngine.UIElements;

namespace Ee4v.UI
{
    public sealed class EmptyStateState
    {
        public EmptyStateState(
            string title,
            string description = null,
            IconState icon = null)
        {
            Title = title ?? string.Empty;
            Description = description ?? string.Empty;
            Icon = icon;
        }

        public string Title { get; }
        public string Description { get; }
        public IconState Icon { get; }
    }

    public sealed class EmptyState : VisualElement
    {
        public EmptyState(EmptyStateState state = null)
        {
            AddToClassList("ee4v-ui-empty-state");
            IconElement = new Icon();
            IconElement.AddToClassList("ee4v-ui-empty-state__icon");
            TitleText = UiTextFactory.Create(
                string.Empty,
                UiClassNames.SectionTitle,
                "ee4v-ui-empty-state__title");
            DescriptionText = UiTextFactory.Create(
                string.Empty,
                UiClassNames.SecondaryText,
                "ee4v-ui-empty-state__description");
            DescriptionText.SetWhiteSpace(WhiteSpace.Normal);
            Actions = new VisualElement();
            Actions.AddToClassList("ee4v-ui-empty-state__actions");

            Add(IconElement);
            Add(TitleText);
            Add(DescriptionText);
            Add(Actions);
            SetState(state ?? new EmptyStateState(string.Empty));
        }

        public Icon IconElement { get; }
        public UiTextElement TitleText { get; }
        public UiTextElement DescriptionText { get; }
        public VisualElement Actions { get; }

        public void SetState(EmptyStateState state)
        {
            state = state ?? new EmptyStateState(string.Empty);
            var hasIcon = state.Icon != null;
            var hasTitle = !string.IsNullOrWhiteSpace(state.Title);
            var hasDescription =
                !string.IsNullOrWhiteSpace(state.Description);

            if (hasIcon)
            {
                IconElement.SetState(state.Icon);
            }

            IconElement.style.display = hasIcon
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            TitleText.SetText(state.Title);
            TitleText.style.display = hasTitle
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            DescriptionText.SetText(state.Description);
            DescriptionText.style.display = hasDescription
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }
    }
}
