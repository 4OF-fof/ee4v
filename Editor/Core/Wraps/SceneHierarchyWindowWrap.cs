using System;
using UnityEditor;
using UnityEngine;

namespace _4OF.ee4v.Core.Wraps {
    internal class SceneHierarchyWindowWrap : WrapBase {
        public static readonly Type Type = typeof(Editor).Assembly.GetType("UnityEditor.SceneHierarchyWindow");
        private static readonly Type TSceneHierarchy = typeof(Editor).Assembly.GetType("UnityEditor.SceneHierarchy");
        private static readonly Type TTreeViewController = typeof(Editor).Assembly.GetType("UnityEditor.IMGUI.Controls.TreeViewController");
        private static readonly Type TTreeViewItem = typeof(Editor).Assembly.GetType("UnityEditor.IMGUI.Controls.TreeViewItem");

        private static readonly Func<object> GetLastInteractedHierarchyWindow = 
            GetStaticProperty<object>(Type, "lastInteractedHierarchyWindow").g;

        private static readonly Func<object, object> GetSceneHierarchy = 
            GetField<object>(Type, "m_SceneHierarchy").g;

        private static readonly Func<object, object> GetTreeView = 
            GetField<object>(TSceneHierarchy, "m_TreeView").g;

        private static readonly Func<object, bool> GetShowingVerticalScrollBar = 
            GetProperty<bool>(TTreeViewController, "showingVerticalScrollBar").g;

        private static readonly Func<object, object[], object> FindItemFunc = 
            GetMethod(TTreeViewController, "FindItem", new[] { typeof(int) });

        private static readonly Action<object, Texture2D> SetIconAction = 
            GetProperty<Texture2D>(TTreeViewItem, "icon").s ?? GetField<Texture2D>(TTreeViewItem, "icon").s;
        
        public object Instance { get; }

        public SceneHierarchyWindowWrap(object instance) {
            Instance = instance;
        }

        public static SceneHierarchyWindowWrap LastInteractedWindow {
            get {
                var instance = GetLastInteractedHierarchyWindow?.Invoke();
                return instance != null ? new SceneHierarchyWindowWrap(instance) : null;
            }
        }

        public bool IsScrollbarVisible {
            get {
                if (Instance == null) return false;
                var sceneHierarchy = GetSceneHierarchy(Instance);
                if (sceneHierarchy == null) return false;
                var treeView = GetTreeView(sceneHierarchy);
                return treeView != null && GetShowingVerticalScrollBar(treeView);
            }
        }

        public void SetItemIcon(int instanceId, Texture2D icon) {
            if (Instance == null) return;
            
            var sceneHierarchy = GetSceneHierarchy(Instance);
            if (sceneHierarchy == null) return;
            
            var treeView = GetTreeView(sceneHierarchy);
            if (treeView == null) return;

            var item = FindItemFunc(treeView, new object[] { instanceId });
            if (item != null) {
                SetIconAction?.Invoke(item, icon);
            }
        }
    }
}