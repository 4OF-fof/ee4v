using UnityEngine.UIElements;

namespace _4OF.ee4v.ProjectExtension.UI.ToolBar {
    public static class WorkspaceContainer {
        public static ScrollView Element() {
            var scrollView = new ScrollView(ScrollViewMode.Horizontal) {
                name = "ee4v-project-toolbar-workspaceContainer-scrollView",
                style = {
                    minWidth = 0,
                    maxWidth = Length.None(),
                    height = 20,
                    flexShrink = 1
                },
                verticalScrollerVisibility = ScrollerVisibility.Hidden,
                horizontalScrollerVisibility = ScrollerVisibility.Hidden
            };

            var workspaceContainer = scrollView.contentContainer;
            workspaceContainer.name = "ee4v-project-toolbar-workspaceContainer";
            workspaceContainer.style.alignItems = Align.Center;
            workspaceContainer.style.height = Length.Percent(100);
            workspaceContainer.style.flexDirection = FlexDirection.Row;

            WorkspaceTabControl(workspaceContainer);

            return scrollView;
        }

        private static void WorkspaceTabControl(VisualElement workspaceContainer) {
            VisualElement potentialDrag = null;

            workspaceContainer.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                if (evt.target is not VisualElement target) return;

                var t = target;
                while (t != null) {
                    if (t.name == "ee4v-project-toolbar-tabContainer-tab-close") return;
                    t = t.parent;
                }

                var tabElement = target;
                while (tabElement != null && tabElement.name != "ee4v-project-toolbar-workspaceContainer-tab")
                    tabElement = tabElement.parent;

                if (tabElement == null || !workspaceContainer.Contains(tabElement)) return;

                potentialDrag = tabElement;
                _ = evt.localPosition;
                evt.StopPropagation();
            }, TrickleDown.TrickleDown);

            workspaceContainer.RegisterCallback<PointerUpEvent>(_ =>
            {
                if (potentialDrag != null && workspaceContainer.Contains(potentialDrag))
                    TabUIManager.SelectTab(potentialDrag);
                potentialDrag = null;
            }, TrickleDown.TrickleDown);
        }
    }
}