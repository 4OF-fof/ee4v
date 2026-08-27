using UnityEngine.UIElements;

namespace Ee4v.UI
{
    public enum ItemRowLayout
    {
        Inline,
        Stacked
    }

    public sealed class ItemRowState
    {
        public ItemRowState(
            string title,
            string description = null,
            IconState icon = null,
            ItemRowLayout layout = ItemRowLayout.Inline)
        {
            Title = title ?? string.Empty;
            Description = description ?? string.Empty;
            Icon = icon;
            Layout = layout;
        }

        public string Title { get; }
        public string Description { get; }
        public IconState Icon { get; }
        public ItemRowLayout Layout { get; }
    }

    public class ItemRow : VisualElement
    {
        public ItemRow(
            ItemRowState state = null,
            string titleTypographyClassName = null)
        {
            AddToClassList("ee4v-ui-item-row");
            Leading = new VisualElement();
            Leading.AddToClassList("ee4v-ui-item-row__leading");
            IconElement = new Icon();
            IconElement.AddToClassList("ee4v-ui-item-row__icon");
            var text = new VisualElement();
            text.AddToClassList("ee4v-ui-item-row__text");
            TitleText = string.IsNullOrWhiteSpace(
                    titleTypographyClassName)
                ? UiTextFactory.Create(
                    string.Empty,
                    "ee4v-ui-item-row__title")
                : UiTextFactory.Create(
                    string.Empty,
                    titleTypographyClassName,
                    "ee4v-ui-item-row__title");
            TitleText.SetWhiteSpace(WhiteSpace.NoWrap);
            DescriptionText = UiTextFactory.Create(
                string.Empty,
                UiClassNames.SecondaryText,
                "ee4v-ui-item-row__description");
            DescriptionText.SetWhiteSpace(WhiteSpace.NoWrap);
            text.Add(TitleText);
            text.Add(DescriptionText);
            Trailing = new VisualElement();
            Trailing.AddToClassList("ee4v-ui-item-row__trailing");

            Add(Leading);
            Add(IconElement);
            Add(text);
            Add(Trailing);
            SetState(state ?? new ItemRowState(string.Empty));
        }

        public VisualElement Leading { get; }
        public Icon IconElement { get; }
        public UiTextElement TitleText { get; }
        public UiTextElement DescriptionText { get; }
        public VisualElement Trailing { get; }

        public void SetState(ItemRowState state)
        {
            state = state ?? new ItemRowState(string.Empty);
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
                "ee4v-ui-item-row--stacked",
                state.Layout == ItemRowLayout.Stacked);
        }
    }
}
