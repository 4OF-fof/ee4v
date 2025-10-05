using UnityEditor;
using UnityEngine;

using System;
using System.Reflection;

namespace _4OF.ee4v.ProjectExtension.Service {
    public static class ReflectionWrapper {
        public static readonly Type ProjectBrowserType = typeof(EditorWindow).Assembly.GetType("UnityEditor.ProjectBrowser");
        
        private static readonly EditorWindow ProjectBrowserWindow;
        private static readonly MethodInfo ShowFolderContentsMethod;
        
        static ReflectionWrapper() {
            ProjectBrowserWindow = EditorWindow.GetWindow(Type.GetType("UnityEditor.ProjectBrowser,UnityEditor.dll"));
            
            if (ProjectBrowserType == null) return;
            ShowFolderContentsMethod = ProjectBrowserType.GetMethod("ShowFolderContents", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(int), typeof(bool) }, null);
        }

        public static EditorWindow GetProjectBrowserWindow() {
            if (ProjectBrowserWindow != null) return ProjectBrowserWindow;
            Debug.LogError("ProjectBrowserWindow is null");
            return null;
        }
        
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