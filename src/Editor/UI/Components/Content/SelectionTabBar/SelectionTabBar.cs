using UnityEngine.UIElements;

namespace Ee4v.UI
{
    public sealed class SelectionTabBar : VisualElement
    {
        public SelectionTabBar()
        {
            AddToClassList("ee4v-ui-selection-tab-bar");
            Items = new VisualElement();
            Items.AddToClassList("ee4v-ui-selection-tab-bar__items");
            Add(Items);
            Tabs = new ScrollView(ScrollViewMode.Horizontal)
            {
                verticalScrollerVisibility = ScrollerVisibility.Hidden,
                horizontalScrollerVisibility = ScrollerVisibility.Hidden
            };
            Tabs.AddToClassList("ee4v-ui-selection-tab-bar__strip");
            Items.Add(Tabs);
        }

        public VisualElement Items { get; }
        public ScrollView Tabs { get; }

        public void SetLeadingTab(VisualElement tab)
        {
            Items.Insert(0, tab);
        }
    }
}
