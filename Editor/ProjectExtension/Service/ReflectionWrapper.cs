using System;
using System.Reflection;
using _4OF.ee4v.Core.i18n;
using UnityEditor;
using UnityEngine;

namespace _4OF.ee4v.ProjectExtension.Service {
    public static class ReflectionWrapper {
        public static readonly Type ProjectBrowserType = Type.GetType("UnityEditor.ProjectBrowser,UnityEditor");
        public static readonly EditorWindow ProjectBrowserWindow = EditorWindow.GetWindow(ProjectBrowserType);

        private static readonly MethodInfo ShowFolderContentsMethod = ProjectBrowserType.GetMethod("ShowFolderContents",
            BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(int), typeof(bool) }, null);

        public static void ShowFolderContents(int instanceId) {
            try {
                if (ShowFolderContentsMethod == null || ProjectBrowserWindow == null) return;
                ShowFolderContentsMethod.Invoke(ProjectBrowserWindow, new object[] { instanceId, false });
            }
            catch (Exception ex) {
                Debug.LogWarning(I18N.Get("Debug.ProjectExtension.ReflectionWarning", ex.Message));
            }
        }

        public static string GetProjectWindowCurrentPath(EditorWindow projectWindow) {
            try {
                if (ProjectBrowserType == null || projectWindow == null) return null;
                var so = new SerializedObject(projectWindow);

                var folders = so.FindProperty("m_SearchFilter.m_Folders");
                if (folders is not { arraySize: > 0 }) folders = so.FindProperty("m_LastFolders");
                if (folders is not { arraySize: > 0 }) return null;
                var folderPath = folders.GetArrayElementAtIndex(0).stringValue;
                return AssetDatabase.IsValidFolder(folderPath) ? folderPath : null;
            }
            catch (Exception ex) {
                Debug.LogWarning(I18N.Get("Debug.ProjectExtension.ReflectionWarning", ex.Message));
                return null;
            }
        }

        public static void SetSearchFilter(string searchText) {
            try {
                if (ProjectBrowserType == null || ProjectBrowserWindow == null) return;
                
                var setSearchMethod = ProjectBrowserType.GetMethod("SetSearch",
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    new[] { typeof(string) },
                    null);
                
                if (setSearchMethod != null) {
                    setSearchMethod.Invoke(ProjectBrowserWindow, new object[] { searchText });
                    ProjectBrowserWindow.Repaint();
                }
                else {
                    Debug.LogWarning(I18N.Get("Debug.ProjectExtension.ReflectionWarning", "SetSearch method not found"));
                }
            }
            catch (Exception ex) {
                Debug.LogWarning(I18N.Get("Debug.ProjectExtension.ReflectionWarning", ex.Message));
            }
        }
    }
}