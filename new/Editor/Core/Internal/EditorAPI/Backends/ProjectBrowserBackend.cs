using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Ee4v.Core.Internal.EditorAPI.Backends
{
    internal static class ProjectBrowserBackend
    {
        private static readonly Type ProjectBrowserType = typeof(Editor).Assembly.GetType("UnityEditor.ProjectBrowser");
        private static readonly MethodInfo ShowFolderContentsMethod = ProjectBrowserType?.GetMethod(
            "ShowFolderContents",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            new[] { typeof(int), typeof(bool) },
            null);
        private static readonly MethodInfo SetSearchMethod = ProjectBrowserType?.GetMethod(
            "SetSearch",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            new[] { typeof(string) },
            null);
        private static readonly MethodInfo ClearSearchMethod = ProjectBrowserType?.GetMethod(
            "ClearSearch",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            Type.EmptyTypes,
            null);

        public static bool TryGetSnapshot(Rect? selectionRect, out ProjectBrowserSnapshot snapshot)
        {
            snapshot = null;

            var window = ResolveTargetWindow();
            if (window == null)
            {
                return false;
            }

            try
            {
                using (var serializedObject = new SerializedObject(window))
                {
                    var folderPath = GetFolderPath(serializedObject);
                    var folderGuid = string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath)
                        ? null
                        : AssetDatabase.AssetPathToGUID(folderPath);
                    var searchText = GetSearchText(serializedObject);
                    var hasSearch = !string.IsNullOrEmpty(searchText);
                    var viewMode = GetViewMode(serializedObject);
                    var orientation = GetOrientation(serializedObject, viewMode, selectionRect);

                    snapshot = new ProjectBrowserSnapshot(
                        folderGuid,
                        folderPath,
                        searchText,
                        hasSearch,
                        viewMode,
                        orientation);
                    return true;
                }
            }
            catch (Exception)
            {
                snapshot = null;
                return false;
            }
        }

        public static bool TryShowFolder(string folderGuid, bool reveal)
        {
            if (string.IsNullOrWhiteSpace(folderGuid) || ShowFolderContentsMethod == null)
            {
                return false;
            }

            var window = ResolveTargetWindow();
            if (window == null)
            {
                return false;
            }

            var folderPath = AssetDatabase.GUIDToAssetPath(folderGuid);
            if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
            {
                return false;
            }

            var folderObject = AssetDatabase.LoadMainAssetAtPath(folderPath);
            if (folderObject == null)
            {
                return false;
            }

            try
            {
                ShowFolderContentsMethod.Invoke(window, new object[] { folderObject.GetInstanceID(), reveal });
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool TrySetSearch(string searchText)
        {
            if (searchText == null || SetSearchMethod == null)
            {
                return false;
            }

            var window = ResolveTargetWindow();
            if (window == null)
            {
                return false;
            }

            try
            {
                SetSearchMethod.Invoke(window, new object[] { searchText });
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool TryClearSearch()
        {
            var window = ResolveTargetWindow();
            if (window == null)
            {
                return false;
            }

            try
            {
                if (ClearSearchMethod != null)
                {
                    ClearSearchMethod.Invoke(window, Array.Empty<object>());
                    return true;
                }

                if (SetSearchMethod == null)
                {
                    return false;
                }

                SetSearchMethod.Invoke(window, new object[] { string.Empty });
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static EditorWindow ResolveTargetWindow()
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

        private static string GetFolderPath(SerializedObject serializedObject)
        {
            var currentFolders = serializedObject.FindProperty("m_SearchFilter.m_Folders");
            var currentFolderPath = GetFirstArrayString(currentFolders);
            if (!string.IsNullOrEmpty(currentFolderPath))
            {
                return currentFolderPath.Replace('\\', '/');
            }

            var lastFolders = serializedObject.FindProperty("m_LastFolders");
            var lastFolderPath = GetFirstArrayString(lastFolders);
            return string.IsNullOrEmpty(lastFolderPath) ? null : lastFolderPath.Replace('\\', '/');
        }

        private static string GetSearchText(SerializedObject serializedObject)
        {
            var searchProperty = serializedObject.FindProperty("m_SearchFilter.m_NameFilter");
            return searchProperty == null ? string.Empty : searchProperty.stringValue ?? string.Empty;
        }

        private static string GetFirstArrayString(SerializedProperty property)
        {
            if (property == null || !property.isArray || property.arraySize == 0)
            {
                return null;
            }

            var first = property.GetArrayElementAtIndex(0);
            return first == null ? null : first.stringValue;
        }

        private static ProjectBrowserViewMode GetViewMode(SerializedObject serializedObject)
        {
            var viewModeProperty = serializedObject.FindProperty("m_ViewMode");
            if (viewModeProperty == null)
            {
                return ProjectBrowserViewMode.Unknown;
            }

            switch (viewModeProperty.intValue)
            {
                case 0:
                    return ProjectBrowserViewMode.OneColumn;
                case 1:
                    return ProjectBrowserViewMode.TwoColumns;
                default:
                    return ProjectBrowserViewMode.Unknown;
            }
        }

        private static ProjectBrowserOrientation GetOrientation(
            SerializedObject serializedObject,
            ProjectBrowserViewMode viewMode,
            Rect? selectionRect)
        {
            if (viewMode == ProjectBrowserViewMode.OneColumn)
            {
                return ProjectBrowserOrientation.Horizontal;
            }

            var gridSizeProperty = serializedObject.FindProperty("m_ListAreaGridSize");
            if (gridSizeProperty != null)
            {
                return gridSizeProperty.floatValue > 20f
                    ? ProjectBrowserOrientation.Vertical
                    : ProjectBrowserOrientation.Horizontal;
            }

            if (selectionRect.HasValue)
            {
                return selectionRect.Value.height > EditorGUIUtility.singleLineHeight * 1.5f
                    ? ProjectBrowserOrientation.Vertical
                    : ProjectBrowserOrientation.Horizontal;
            }

            return ProjectBrowserOrientation.Unknown;
        }
    }
}
