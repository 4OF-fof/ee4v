using UnityEngine.UIElements;

namespace Ee4v.UI
{
    public class PreviewContainer : VisualElement
    {
        public PreviewContainer()
        {
            AddToClassList("ee4v-ui-preview-container");
            Content = new VisualElement();
            Content.AddToClassList("ee4v-ui-preview-container__content");
            Placeholder = new VisualElement();
            Placeholder.AddToClassList(
                "ee4v-ui-preview-container__placeholder");
            Overlay = new VisualElement();
            Overlay.AddToClassList("ee4v-ui-preview-container__overlay");
            Overlay.pickingMode = PickingMode.Ignore;
            hierarchy.Add(Content);
            hierarchy.Add(Placeholder);
            hierarchy.Add(Overlay);
            SetHasContent(false);
        }

        public VisualElement Content { get; }
        public VisualElement Placeholder { get; }
        public VisualElement Overlay { get; }

        public override VisualElement contentContainer => Content;

        public void SetHasContent(bool hasContent)
        {
            Content.style.display = hasContent
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            Placeholder.style.display = hasContent
                ? DisplayStyle.None
                : DisplayStyle.Flex;
        }
    }
}
