using UnityEditor;

namespace Ee4v.Core.Injector
{
    [InitializeOnLoad]
    internal static class InjectorEditorLifecycle
    {
        static InjectorEditorLifecycle()
        {
            EditorApplication.hierarchyWindowItemOnGUI -=
                InjectorApi.DrawHierarchyItem;
            EditorApplication.hierarchyWindowItemOnGUI +=
                InjectorApi.DrawHierarchyItem;

            EditorApplication.projectWindowItemOnGUI -=
                InjectorApi.DrawProjectItem;
            EditorApplication.projectWindowItemOnGUI +=
                InjectorApi.DrawProjectItem;

            EditorApplication.update -= InjectorApi.UpdateVisualHosts;
            EditorApplication.update += InjectorApi.UpdateVisualHosts;

            EditorApplication.modifierKeysChanged -=
                RepaintItemWindows;
            EditorApplication.modifierKeysChanged +=
                RepaintItemWindows;
        }

        private static void RepaintItemWindows()
        {
            EditorApplication.RepaintHierarchyWindow();
            EditorApplication.RepaintProjectWindow();
        }
    }
}
