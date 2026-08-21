using UnityEngine.UIElements;

namespace Ee4v.UI
{
    public sealed class PreviewSurface : VisualElement
    {
        public PreviewSurface()
        {
            AddToClassList("ee4v-ui-preview-surface");
            Content = new VisualElement();
            Content.AddToClassList("ee4v-ui-preview-surface__content");
            Placeholder = new VisualElement();
            Placeholder.AddToClassList(
                "ee4v-ui-preview-surface__placeholder");
            Overlay = new VisualElement();
            Overlay.AddToClassList("ee4v-ui-preview-surface__overlay");
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
