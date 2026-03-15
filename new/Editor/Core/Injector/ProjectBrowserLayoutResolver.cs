using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Ee4v.Injector
{
    internal static class ProjectBrowserLayoutResolver
    {
        private static readonly Type ProjectBrowserType = typeof(Editor).Assembly.GetType("UnityEditor.ProjectBrowser");

        public static ProjectItemViewMode GetViewMode()
        {
            var projectBrowser = GetCurrentProjectBrowser();
            if (projectBrowser == null)
            {
                return ProjectItemViewMode.Unknown;
            }

            using (var serializedObject = new SerializedObject(projectBrowser))
            {
                var viewModeProperty = serializedObject.FindProperty("m_ViewMode");
                if (viewModeProperty == null)
                {
                    return ProjectItemViewMode.Unknown;
                }

                switch (viewModeProperty.intValue)
                {
                    case 0:
                        return ProjectItemViewMode.OneColumn;
                    case 1:
                        return ProjectItemViewMode.TwoColumns;
                    default:
                        return ProjectItemViewMode.Unknown;
                }
            }
        }

        public static ProjectItemOrientation GetOrientation(Rect selectionRect, ProjectItemViewMode viewMode)
        {
            if (viewMode == ProjectItemViewMode.OneColumn)
            {
                return ProjectItemOrientation.Horizontal;
            }

            var projectBrowser = GetCurrentProjectBrowser();
            if (projectBrowser != null)
            {
                using (var serializedObject = new SerializedObject(projectBrowser))
                {
                    var gridSizeProperty = serializedObject.FindProperty("m_ListAreaGridSize");
                    if (gridSizeProperty != null)
                    {
                        return gridSizeProperty.floatValue > 20f
                            ? ProjectItemOrientation.Vertical
                            : ProjectItemOrientation.Horizontal;
                    }
                }
            }

            return selectionRect.height > EditorGUIUtility.singleLineHeight * 1.5f
                ? ProjectItemOrientation.Vertical
                : ProjectItemOrientation.Horizontal;
        }

        private static EditorWindow GetCurrentProjectBrowser()
        {
            if (IsProjectBrowser(EditorWindow.focusedWindow))
            {
                return EditorWindow.focusedWindow;
            }

            if (IsProjectBrowser(EditorWindow.mouseOverWindow))
            {
                return EditorWindow.mouseOverWindow;
            }

            return ProjectBrowserType == null
                ? null
                : Resources.FindObjectsOfTypeAll(ProjectBrowserType).OfType<EditorWindow>().FirstOrDefault();
        }

        private static bool IsProjectBrowser(EditorWindow window)
        {
            return window != null && ProjectBrowserType != null && ProjectBrowserType.IsInstanceOfType(window);
        }
    }
}
