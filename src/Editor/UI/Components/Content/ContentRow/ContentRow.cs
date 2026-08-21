using UnityEngine.UIElements;

namespace Ee4v.UI
{
    public enum ContentRowLayout
    {
        Inline,
        Stacked
    }

    public sealed class ContentRowState
    {
        public ContentRowState(
            string title,
            string description = null,
            IconState icon = null,
            ContentRowLayout layout = ContentRowLayout.Inline)
        {
            Title = title ?? string.Empty;
            Description = description ?? string.Empty;
            Icon = icon;
            Layout = layout;
        }

        public string Title { get; }
        public string Description { get; }
        public IconState Icon { get; }
        public ContentRowLayout Layout { get; }
    }

    public class ContentRow : VisualElement
    {
        public ContentRow(
            ContentRowState state = null,
            string titleTypographyClassName = null)
        {
            AddToClassList("ee4v-ui-content-row");
            Leading = new VisualElement();
            Leading.AddToClassList("ee4v-ui-content-row__leading");
            IconElement = new Icon();
            IconElement.AddToClassList("ee4v-ui-content-row__icon");
            var text = new VisualElement();
            text.AddToClassList("ee4v-ui-content-row__text");
            TitleText = string.IsNullOrWhiteSpace(
                    titleTypographyClassName)
                ? UiTextFactory.Create(
                    string.Empty,
                    "ee4v-ui-content-row__title")
                : UiTextFactory.Create(
                    string.Empty,
                    titleTypographyClassName,
                    "ee4v-ui-content-row__title");
            TitleText.SetWhiteSpace(WhiteSpace.NoWrap);
            DescriptionText = UiTextFactory.Create(
                string.Empty,
                UiClassNames.SecondaryText,
                "ee4v-ui-content-row__description");
            DescriptionText.SetWhiteSpace(WhiteSpace.NoWrap);
            text.Add(TitleText);
            text.Add(DescriptionText);
            Trailing = new VisualElement();
            Trailing.AddToClassList("ee4v-ui-content-row__trailing");

            Add(Leading);
            Add(IconElement);
            Add(text);
            Add(Trailing);
            SetState(state ?? new ContentRowState(string.Empty));
        }

        public VisualElement Leading { get; }
        public Icon IconElement { get; }
        public UiTextElement TitleText { get; }
        public UiTextElement DescriptionText { get; }
        public VisualElement Trailing { get; }

        public void SetState(ContentRowState state)
        {
            state = state ?? new ContentRowState(string.Empty);
            var hasIcon = state.Icon != null;
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
            DescriptionText.SetText(state.Description);
            DescriptionText.style.display = hasDescription
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            EnableInClassList(
                "ee4v-ui-content-row--stacked",
                state.Layout == ContentRowLayout.Stacked);
        }
    }
}
