using System;
using System.Reflection;
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
                Debug.LogWarning($"{ex.Message}");
            }
        }
    }
}