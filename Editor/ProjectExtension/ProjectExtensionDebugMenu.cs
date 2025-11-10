using _4OF.ee4v.ProjectExtension.Data;
using _4OF.ee4v.ProjectExtension.UI.ToolBar._Component.Tab;
using UnityEditor;

namespace _4OF.ee4v.ProjectExtension {
    public static class ProjectExtensionDebugMenu {
        [MenuItem("Debug/Add Workspace Tab")]
        private static void AddWorkspaceTab() {
            var workspaceTab = WorkspaceTab.Element("Assets", "Workspace");
            TabListController.AddWorkspaceTab(workspaceTab);
            TabListController.SelectTab(workspaceTab);
        }
    }
}
