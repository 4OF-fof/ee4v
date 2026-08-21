using UnityEngine.UIElements;

namespace Ee4v.UI
{
    public class LabeledContentRow : VisualElement
    {
        public LabeledContentRow(string label = null)
        {
            AddToClassList("ee4v-ui-labeled-content-row");
            LabelText = UiTextFactory.Create(
                string.Empty,
                UiClassNames.FormLabel,
                "ee4v-ui-labeled-content-row__label");
            Content = new VisualElement();
            Content.AddToClassList(
                "ee4v-ui-labeled-content-row__content");
            Actions = new VisualElement();
            Actions.AddToClassList(
                "ee4v-ui-labeled-content-row__actions");
            Add(LabelText);
            Add(Content);
            Add(Actions);
            SetLabel(label);
        }

        public UiTextElement LabelText { get; }
        public VisualElement Content { get; }
        public VisualElement Actions { get; }

        public void SetLabel(string label)
        {
            label = label ?? string.Empty;
            LabelText.SetText(label);
            LabelText.style.display = string.IsNullOrWhiteSpace(label)
                ? DisplayStyle.None
                : DisplayStyle.Flex;
        }
    }
}
