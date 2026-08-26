using UnityEngine.UIElements;

namespace Ee4v.UI
{
    public sealed class Badge : VisualElement
    {
        public Badge(string text = null)
        {
            AddToClassList("ee4v-ui-badge");
            TextElement = UiTextFactory.Create(
                string.Empty,
                UiClassNames.SecondaryText,
                "ee4v-ui-badge__text");
            TextElement.pickingMode = PickingMode.Ignore;
            Add(TextElement);
            SetText(text);
        }

        public UiTextElement TextElement { get; }

        public void SetText(string text)
        {
            text = text ?? string.Empty;
            TextElement.SetText(text);
            style.display = string.IsNullOrWhiteSpace(text)
                ? DisplayStyle.None
                : DisplayStyle.Flex;
        }
    }
}
