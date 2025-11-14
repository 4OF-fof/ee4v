using System.Collections.Generic;
using System.IO;
using System.Linq;
using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.HierarchyExtension.Data.Schema;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace _4OF.ee4v.HierarchyExtension.Data {
    public static class SceneListController {
        private static SceneListObject _asset;
        private const string AssetPath = "Assets/4OF/ee4v/UserData/SceneList.asset";

        public static List<string> ScenePathList {
            get {
                Initialize();
                var scenes = _asset?.SceneList.Where(s => !s.isIgnored).ToList() ??
                    new List<SceneListObject.SceneContent>();

                var openScenePaths = new HashSet<string>();
                for (var i = 0; i < SceneManager.sceneCount; ++i) {
                    var s = SceneManager.GetSceneAt(i);
                    if (!string.IsNullOrEmpty(s.path)) openScenePaths.Add(s.path);
                }

                var openScenes = scenes.Where(s => openScenePaths.Contains(s.path)).OrderBy(s => scenes.IndexOf(s));
                var favorites = scenes.Where(s => !openScenePaths.Contains(s.path) && s.isFavorite)
                    .OrderBy(s => scenes.IndexOf(s));
                var others = scenes.Where(s => !openScenePaths.Contains(s.path) && !s.isFavorite)
                    .OrderBy(s => scenes.IndexOf(s));
                return openScenes.Concat(favorites).Concat(others).Select(s => s.path).ToList();
            }
        }

        public static SceneListObject GetInstance() {
            if (_asset == null) _asset = LoadOrCreate();
            return _asset;
        }

        private static void Initialize() {
            if (_asset == null) _asset = LoadOrCreate();
        }

        private static SceneListObject LoadOrCreate() {
            var sceneListObject = AssetDatabase.LoadAssetAtPath<SceneListObject>(AssetPath);
            if (sceneListObject != null) {
                _asset = sceneListObject;
                return sceneListObject;
            }

            var dir = Path.GetDirectoryName(AssetPath);
            if (!Directory.Exists(dir) && !string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            sceneListObject = ScriptableObject.CreateInstance<SceneListObject>();
            sceneListObject.sceneList = new List<SceneListObject.SceneContent>();
            AssetDatabase.CreateAsset(sceneListObject, AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.LogWarning(I18N.Get("Debug.HierarchyExtension.ScheneListObject.NotFound", AssetPath));
            _asset = sceneListObject;
            return sceneListObject;
        }

        private static void Add(string path, bool isIgnored = false, bool isFavorite = false) {
            Initialize();
            var entry = new SceneListObject.SceneContent { path = path, isIgnored = isIgnored, isFavorite = isFavorite };
            _asset.sceneList.Add(entry);
            EditorUtility.SetDirty(_asset);
        }

        private static void Insert(int index, string path, bool isIgnored = false, bool isFavorite = false) {
            Initialize();
            var entry = new SceneListObject.SceneContent { path = path, isIgnored = isIgnored, isFavorite = isFavorite };
            index = Mathf.Clamp(index, 0, _asset.sceneList.Count);
            _asset.sceneList.Insert(index, entry);
            EditorUtility.SetDirty(_asset);
        }

        private static void Remove(int index) {
            Initialize();
            if (index < 0 || index >= _asset.sceneList.Count) return;
            _asset.sceneList.RemoveAt(index);
            EditorUtility.SetDirty(_asset);
        }

        private static void UpdateScene(int index, string path = null, bool? isIgnored = null, bool? isFavorite = null) {
            Initialize();
            if (index < 0 || index >= _asset.sceneList.Count) return;
            if (path != null) _asset.sceneList[index].path = path;
            if (isIgnored.HasValue) _asset.sceneList[index].isIgnored = isIgnored.Value;
            if (isFavorite.HasValue) _asset.sceneList[index].isFavorite = isFavorite.Value;
            EditorUtility.SetDirty(_asset);
        }

        public static void Move(int fromIndex, int toIndex) {
            Initialize();
            if (fromIndex < 0 || fromIndex >= _asset.SceneList.Count) return;
            if (toIndex < 0 || toIndex >= _asset.SceneList.Count) return;
            if (fromIndex == toIndex) return;
            var item = _asset.SceneList[fromIndex];
            Remove(fromIndex);
            Insert(toIndex, item.path, item.isIgnored, item.isFavorite);
        }

        public static bool IsFavorite(string path) {
            Initialize();
            var scene = _asset?.SceneList.FirstOrDefault(s => s.path == path);
            return scene?.isFavorite ?? false;
        }

        public static void ToggleFavorite(string path) {
            Initialize();
            var index = -1;
            for (var i = 0; i < _asset.SceneList.Count; i++)
                if (_asset.SceneList[i].path == path) {
                    index = i;
                    break;
                }

            if (index < 0) return;
            var currentValue = _asset.SceneList[index].isFavorite;
            UpdateScene(index, isFavorite: !currentValue);
        }

        public static void MoveToTop(string path) {
            Initialize();
            var index = -1;
            for (var i = 0; i < _asset.SceneList.Count; i++)
                if (_asset.SceneList[i].path == path) {
                    index = i;
                    break;
                }

            if (index is < 0 or 0) return;

            var item = _asset.SceneList[index];
            Remove(index);
            Insert(0, item.path, item.isIgnored, item.isFavorite);
        }

        public static void SceneListRegister() {
            var guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
            var currentPathList = guids.Select(AssetDatabase.GUIDToAssetPath).Where(p => !string.IsNullOrEmpty(p))
                .ToList();
            Initialize();

            for (var i = _asset.SceneList.Count - 1; i >= 0; i--) {
                var path = _asset.SceneList[i].path;
                if (currentPathList.All(p => p != path)) Remove(i);
            }

            foreach (var path in currentPathList.Where(path => _asset.SceneList.All(s => s.path != path))) Add(path);
        }
    }
}