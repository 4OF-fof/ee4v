using UnityEngine.UIElements;

namespace _4OF.ee4v.ProjectExtension.UI.ToolBar {
    public static class ProjectToolBar {
        public static VisualElement Element() {
            var workspaceContainer = WorkspaceContainer.Element();
            var tabContainer = TabContainer.Element();

            var projectToolBar = new VisualElement {
                name = "ee4v-project-toolbar",
                style = {
                    flexDirection = FlexDirection.Row,
                    marginLeft = 36,
                    marginRight = 470,
                    height = 20,
                    overflow = Overflow.Hidden
                }
            };

            projectToolBar.Add(workspaceContainer);
            projectToolBar.Add(tabContainer);

            return projectToolBar;
        }
    }
}