using UnityEngine.UIElements;

namespace _4OF.ee4v.ProjectExtension.Toolbar.Component {
    public class ProjectToolBar : VisualElement {
        public ProjectToolBar() {
            var workspaceContainer = WorkspaceContainer.Element();
            var tabContainer = new TabContainer();

            name = "ee4v-project-toolbar";
            style.flexDirection = FlexDirection.Row;
            style.marginLeft = 36;
            style.marginRight = 470;
            style.height = 20;
            style.overflow = Overflow.Hidden;

            Add(workspaceContainer);
            Add(tabContainer);
        }
    }
}