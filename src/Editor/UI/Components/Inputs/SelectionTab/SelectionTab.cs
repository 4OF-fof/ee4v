using Ee4v.Core.I18n;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    public enum SelectionTabVariant
    {
        Default,
        Primary
    }

    public sealed class SelectionTab : VisualElement
    {
        public SelectionTab(string title, SelectionTabVariant variant = SelectionTabVariant.Default)
        {
            AddToClassList("ee4v-ui-selection-tab");
            AddToClassList(variant == SelectionTabVariant.Primary
                ? "ee4v-ui-selection-tab--primary"
                : "ee4v-ui-selection-tab--default");
            var label = UiTextFactory.Create(title,
                "ee4v-ui-selection-tab-name");
            label.SetFontSize(UiTypographyTokens.SubtitleFontSize);
            label.SetTextAlign(TextAnchor.MiddleLeft);
            label.tooltip = title;
            Add(label);
        }

        public void SetSelected(bool selected)
        {
            EnableInClassList("ee4v-ui-selection-tab--selected", selected);
        }
    }
}
