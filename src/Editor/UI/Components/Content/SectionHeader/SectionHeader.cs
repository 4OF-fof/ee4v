using UnityEngine.UIElements;

namespace Ee4v.UI
{
    public sealed class SectionHeader : VisualElement
    {
        public SectionHeader(
            string title = null,
            string description = null)
        {
            AddToClassList("ee4v-ui-section-header");
            var text = new VisualElement();
            text.AddToClassList("ee4v-ui-section-header__text");
            TitleText = UiTextFactory.Create(
                string.Empty,
                UiClassNames.SectionTitle,
                "ee4v-ui-section-header__title");
            DescriptionText = UiTextFactory.Create(
                string.Empty,
                UiClassNames.SecondaryText,
                "ee4v-ui-section-header__description");
            DescriptionText.SetWhiteSpace(WhiteSpace.Normal);
            text.Add(TitleText);
            text.Add(DescriptionText);

            Actions = new VisualElement();
            Actions.AddToClassList("ee4v-ui-section-header__actions");
            Add(text);
            Add(Actions);
            SetTitle(title);
            SetDescription(description);
        }

        public UiTextElement TitleText { get; }
        public UiTextElement DescriptionText { get; }
        public VisualElement Actions { get; }

        public void SetTitle(string title)
        {
            title = title ?? string.Empty;
            TitleText.SetText(title);
            TitleText.style.display = string.IsNullOrWhiteSpace(title)
                ? DisplayStyle.None
                : DisplayStyle.Flex;
        }

        public void SetDescription(string description)
        {
            description = description ?? string.Empty;
            DescriptionText.SetText(description);
            DescriptionText.style.display =
                string.IsNullOrWhiteSpace(description)
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;
        }
    }
}
