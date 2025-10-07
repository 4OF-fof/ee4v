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
            SaveAsset(SceneListObject.GetInstance());
            SaveAsset(FolderStyleObject.GetInstance());
            SaveAsset(TabListObject.GetInstance());
        }

        private static void SaveAsset(ScriptableObject asset) {
            if (asset == null) return;
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
        }
    }
}