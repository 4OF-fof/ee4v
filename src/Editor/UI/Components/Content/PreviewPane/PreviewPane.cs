using UnityEngine.UIElements;

namespace Ee4v.UI
{
    public sealed class PreviewPane : InfoCard
    {
        public PreviewPane(string title) : base(new InfoCardState(title), UiClassNames.SectionTitle)
        {
            RemoveFromClassList("ee4v-ui-info-card");
            AddToClassList("ee4v-ui-preview-pane");
            Header.RemoveFromClassList("ee4v-ui-info-card__header");
            Header.RemoveFromClassList("ee4v-ui-info-card__header--single-line");
            Header.AddToClassList("ee4v-ui-preview-pane__toolbar");
            TitleText.AddToClassList("ee4v-ui-preview-pane__title");
            HeaderRight.RemoveFromClassList("ee4v-ui-info-card__header-right");
            HeaderRight.AddToClassList("ee4v-ui-preview-pane__actions");
            Body.RemoveFromClassList("ee4v-ui-info-card__body");
            Body.AddToClassList("ee4v-ui-preview-pane__content");
            RefreshLayout();
        }

        public VisualElement Actions => HeaderRight;
        public VisualElement Content => Body;

        public override void RefreshLayout()
        {
            base.RefreshLayout();
            Body.RemoveFromClassList("ee4v-ui-info-card__body--compact");
            Body.RemoveFromClassList("ee4v-ui-info-card__body--with-header");
            Body.style.display = DisplayStyle.Flex;
            Header.style.display = DisplayStyle.Flex;
            HeaderRight.style.display = DisplayStyle.Flex;
        }
    }
}
