using _4OF.ee4v.HierarchyExtension.Data;
using _4OF.ee4v.ProjectExtension.Data;
using UnityEditor;
using UnityEngine;

namespace _4OF.ee4v.Core.Data {
    [InitializeOnLoad]
    public static class EditorLifecycleHandler {
        static EditorLifecycleHandler() {
            EditorApplication.quitting += OnEditorQuitting;
        }

        private static void OnEditorQuitting() {
            SaveAsset(SceneListController.GetInstance());
            SaveAsset(FolderStyleController.GetInstance());
            SaveAsset(TabListController.GetInstance());
        }

        private static void SaveAsset(ScriptableObject asset) {
            if (asset == null) return;
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
        }
    }
}