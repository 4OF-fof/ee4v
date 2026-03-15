using System;
using UnityEditor;
using Object = UnityEngine.Object;

namespace _4OF.ee4v.Core.Wraps {
    internal class ProjectBrowserWrap : WrapBase {
        public static readonly Type Type = typeof(Editor).Assembly.GetType("UnityEditor.ProjectBrowser");

        private static readonly Func<object, object[], object> ShowFolderContentsFunc =
            GetMethod(Type, "ShowFolderContents", new[] { typeof(int), typeof(bool) });

        private static readonly Action<object, string> SetSearchAction =
            GetAction<string>(Type, "SetSearch");

        private static readonly Action<object> ClearSearchAction =
            GetAction(Type, "ClearSearch");

        private static readonly Func<object, object> GetSearchFilter =
            GetField<object>(Type, "m_SearchFilter").g;

        public ProjectBrowserWrap(object instance) {
            Instance = instance;
        }

        public object Instance { get; }

        public static ProjectBrowserWrap GetWindow() {
            var win = EditorWindow.GetWindow(Type);
            return win ? new ProjectBrowserWrap(win) : null;
        }

        public void ShowFolderContents(int instanceId, bool reveal) {
            try {
                ShowFolderContentsFunc?.Invoke(Instance, new object[] { instanceId, reveal });
            }
            catch (Exception) {
                // Ignore
            }
        }

        public void SetSearch(string searchString) {
            SetSearchAction?.Invoke(Instance, searchString);
        }

        public void ClearSearch() {
            try {
                ClearSearchAction?.Invoke(Instance);
            }
            catch (Exception) {
                // Ignore
            }
        }

        public string GetCurrentFolderPath() {
            if (Instance == null) return null;

            var searchFilter = GetSearchFilter(Instance);
            if (searchFilter != null) {
                using var so = new SerializedObject((Object)Instance);
                var mFolders = so.FindProperty("m_SearchFilter.m_Folders");
                if (mFolders != null && mFolders.arraySize > 0) return mFolders.GetArrayElementAtIndex(0).stringValue;
            }

            using var so2 = new SerializedObject((Object)Instance);
            var lastFolders = so2.FindProperty("m_LastFolders");
            return lastFolders != null && lastFolders.arraySize > 0
                ? lastFolders.GetArrayElementAtIndex(0).stringValue
                : null;
        }
    }
}