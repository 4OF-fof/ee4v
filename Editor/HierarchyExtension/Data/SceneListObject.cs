using System;
using System.Collections.Generic;
using System.IO;
using _4OF.ee4v.Core.i18n;
using UnityEditor;
using UnityEngine;

namespace _4OF.ee4v.HierarchyExtension.Data {
    public class SceneListObject : ScriptableObject {
        private static SceneListObject _instance;

        [SerializeField] private List<SceneContent> sceneList = new();

        public IReadOnlyList<SceneContent> SceneList => sceneList;

        public void Add(string path, bool isIgnored = false, bool isFavorite = false) {
            var entry = new SceneContent { path = path, isIgnored = isIgnored, isFavorite = isFavorite };
            sceneList.Add(entry);
        }

        public void Insert(int index, string path, bool isIgnored = false, bool isFavorite = false) {
            var entry = new SceneContent { path = path, isIgnored = isIgnored, isFavorite = isFavorite };
            index = Mathf.Clamp(index, 0, sceneList.Count);
            sceneList.Insert(index, entry);
        }

        public void Remove(int index) {
            if (index < 0 || index >= sceneList.Count) return;
            sceneList.RemoveAt(index);
        }

        public void UpdateScene(int index, string path = null, bool? isIgnored = null, bool? isFavorite = null) {
            if (index < 0 || index >= sceneList.Count) return;
            if (path != null) sceneList[index].path = path;
            if (isIgnored.HasValue) sceneList[index].isIgnored = isIgnored.Value;
            if (isFavorite.HasValue) sceneList[index].isFavorite = isFavorite.Value;
        }

        public static SceneListObject GetInstance() {
            if (_instance == null) _instance = LoadOrCreate();
            return _instance;
        }

        public static SceneListObject LoadOrCreate() {
            const string path = "Assets/4OF/ee4v/UserData/SceneList.asset";
            var sceneListObject = AssetDatabase.LoadAssetAtPath<SceneListObject>(path);
            if (sceneListObject != null) {
                _instance = sceneListObject;
                return sceneListObject;
            }

            var dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir) && !string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            sceneListObject = CreateInstance<SceneListObject>();
            sceneListObject.sceneList = new List<SceneContent>();
            AssetDatabase.CreateAsset(sceneListObject, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.LogWarning(I18N.Get("Debug.HierarchyExtension.ScheneListObject.NotFound", path));
            _instance = sceneListObject;
            return sceneListObject;
        }

        [Serializable]
        public class SceneContent {
            public string path;
            public bool isIgnored;
            public bool isFavorite;
        }
    }
}