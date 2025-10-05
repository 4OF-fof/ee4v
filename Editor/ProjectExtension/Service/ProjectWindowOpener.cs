using UnityEditor;
using UnityEngine;

namespace _4OF.ee4v.ProjectExtension.Service {
    public static class ProjectWindowOpener {
        public static void OpenFolderInProject(string path) {
            var folderObject = AssetDatabase.LoadAssetAtPath<DefaultAsset>(path);
            if (folderObject == null) return;
            
            ReflectionWrapper.ShowFolderContents(folderObject.GetInstanceID());
            Selection.activeObject = null;
            GUI.FocusControl(null);
        }
    }
}