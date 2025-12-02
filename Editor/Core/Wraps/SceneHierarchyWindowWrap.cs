using System;
using UnityEditor;
using UnityEngine;

namespace _4OF.ee4v.Core.Wraps {
    internal class SceneHierarchyWindowWrap : WrapBase {
        private static readonly Type TSceneHierarchyWindow =
            typeof(Editor).Assembly.GetType("UnityEditor.SceneHierarchyWindow");

        private static readonly (Func<object> g, Action<object> s) PILastInteractedHierarchyWindow =
            GetStaticProperty(TSceneHierarchyWindow, "lastInteractedHierarchyWindow");

        private static readonly (Func<object, object> g, Action<object, object> s) FiMSceneHierarchy =
            GetField(TSceneHierarchyWindow, "m_SceneHierarchy");

        private static readonly Type TSceneHierarchy = typeof(Editor).Assembly.GetType("UnityEditor.SceneHierarchy");

        private static readonly (Func<object, object> g, Action<object, object> s) FiMTreeView =
            GetField(TSceneHierarchy, "m_TreeView");

        private static readonly Type TTreeViewController =
            typeof(Editor).Assembly.GetType("UnityEditor.IMGUI.Controls.TreeViewController");

        private static readonly (Func<object, object> g, Action<object, object> s) PIShowingVerticalScrollBar =
            GetProperty(TTreeViewController, "showingVerticalScrollBar");

        private static readonly Func<object, object[], object> MiFindItem =
            GetMethod(TTreeViewController, "FindItem", new[] { typeof(int) });

        private static readonly Type TTreeViewItem =
            typeof(Editor).Assembly.GetType("UnityEditor.IMGUI.Controls.TreeViewItem");

        private static readonly (Func<object, object> g, Action<object, object> s) PIIcon =
            GetProperty(TTreeViewItem, "icon");

        private static readonly (Func<object, object> g, Action<object, object> s) FiIcon =
            GetField(TTreeViewItem, "icon");

        public static object LastInteractedWindow => PILastInteractedHierarchyWindow.g();

        public static bool IsScrollbarVisible {
            get {
                var window = LastInteractedWindow;
                if (window == null) return false;

                var sceneHierarchy = FiMSceneHierarchy.g(window);
                if (sceneHierarchy == null) return false;

                var treeView = FiMTreeView.g(sceneHierarchy);
                if (treeView == null) return false;

                return (bool)PIShowingVerticalScrollBar.g(treeView);
            }
        }

        public static void SetItemIcon(int instanceId, Texture2D icon) {
            var window = LastInteractedWindow;
            if (window == null) return;

            var sceneHierarchy = FiMSceneHierarchy.g(window);
            if (sceneHierarchy == null) return;

            var treeView = FiMTreeView.g(sceneHierarchy);
            if (treeView == null) return;

            var item = MiFindItem(treeView, new object[] { instanceId });
            if (item == null) return;

            if (PIIcon.s != null)
                PIIcon.s(item, icon);
            else if (FiIcon.s != null)
                FiIcon.s(item, icon);
        }
    }
}