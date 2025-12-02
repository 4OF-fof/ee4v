using System;
using System.Reflection;
using UnityEditor;
using Object = UnityEngine.Object;

namespace _4OF.ee4v.Core.Wraps {
    internal class ProjectBrowserWrap : WrapBase {
        public static readonly Type Type = typeof(Editor).Assembly.GetType("UnityEditor.ProjectBrowser");

        private static readonly MethodInfo MiShowFolderContents = Type.GetMethod("ShowFolderContents",
            BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(int), typeof(bool) }, null);

        private static readonly MethodInfo MiSetSearch = Type.GetMethod("SetSearch",
            BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(string) }, null);

        private static readonly MethodInfo MiClearSearch = Type.GetMethod("ClearSearch",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        private static readonly (Func<object, object> g, Action<object, object> s) FiMSearchFilter =
            GetField(Type, "m_SearchFilter");

        public ProjectBrowserWrap(object instance) {
            Instance = instance;
        }

        public object Instance { get; }

        public static ProjectBrowserWrap GetWindow() {
            var win = EditorWindow.GetWindow(Type);
            return win ? new ProjectBrowserWrap(win) : null;
        }

        public void ShowFolderContents(int instanceId, bool reveal) {
            MiShowFolderContents?.Invoke(Instance, new object[] { instanceId, reveal });
        }

        public void SetSearch(string searchString) {
            MiSetSearch?.Invoke(Instance, new object[] { searchString });
        }

        public void ClearSearch() {
            MiClearSearch?.Invoke(Instance, null);
        }

        public string GetCurrentFolderPath() {
            if (Instance == null) return null;

            var searchFilter = FiMSearchFilter.g(Instance);
            if (searchFilter != null) {
                var mFolders = new SerializedObject((Object)Instance).FindProperty("m_SearchFilter.m_Folders");
                if (mFolders is { arraySize: > 0 }) return mFolders.GetArrayElementAtIndex(0).stringValue;
            }

            var lastFolders = new SerializedObject((Object)Instance).FindProperty("m_LastFolders");
            return lastFolders is { arraySize: > 0 } ? lastFolders.GetArrayElementAtIndex(0).stringValue : null;
        }
    }
}