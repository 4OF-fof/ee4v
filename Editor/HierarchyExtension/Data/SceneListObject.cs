using UnityEngine;
using UnityEditor;

using System.Collections.Generic;

namespace _4OF.ee4v.HierarchyExtension.Data {
    public class SceneListObject : ScriptableObject {
        [SerializeField] private List<SceneContent> sceneList = new();
        
        public IReadOnlyList<SceneContent> SceneList => sceneList;

        [System.Serializable]
        public class SceneContent {
            public string path;
            public bool isIgnored;
        }
        
        public void Add(string path, bool isIgnored = false) {
            var entry = new SceneContent { path = path, isIgnored = isIgnored };
            sceneList.Add(entry);
        }
        
        public void Insert(int index, string path, bool isIgnored = false) {
            var entry = new SceneContent { path = path, isIgnored = isIgnored };
            index = Mathf.Clamp(index, 0, sceneList.Count);
            sceneList.Insert(index, entry);
        }
        
        public void Remove(int index) {
            if (index < 0 || index >= sceneList.Count) return;
            sceneList.RemoveAt(index);
        }
        
        public void UpdateScene(int index, string path, bool isIgnored) {
            if (index < 0 || index >= sceneList.Count) return;
            sceneList[index].path = path;
            sceneList[index].isIgnored = isIgnored;
        }
        
        public static SceneListObject LoadOrCreate() {
            var temp = CreateInstance<SceneListObject>();
            var scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(temp));
            DestroyImmediate(temp);
            var path = scriptPath.Replace("HierarchyExtension/Data/SceneListObject.cs", "UserData/SceneList.asset");
            var sceneListObject = AssetDatabase.LoadAssetAtPath<SceneListObject>(path);
            if (sceneListObject != null) return sceneListObject;
            
            var dir = System.IO.Path.GetDirectoryName(path);
            if (!System.IO.Directory.Exists(dir) && !string.IsNullOrEmpty(dir)) System.IO.Directory.CreateDirectory(dir);
            sceneListObject = CreateInstance<SceneListObject>();
            sceneListObject.sceneList = new List<SceneContent>();
            AssetDatabase.CreateAsset(sceneListObject, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.LogWarning($"SceneListObject not found at {path}. Creating new one.");
            return sceneListObject;
        }
    }
}