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
        private const string AssetPath = "Assets/4OF/ee4v/UserData/SceneList.asset";
        private static SceneList _asset;

        public static List<string> ScenePathList {
            get {
                Initialize();
                var scenes = _asset?.Contents.Where(s => !s.isIgnored).ToList() ??
                    new List<SceneList.SceneContent>();

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

        public static SceneList GetInstance() {
            if (_asset == null) _asset = LoadOrCreate();
            return _asset;
        }

        private static void Initialize() {
            if (_asset == null) _asset = LoadOrCreate();
        }

        private static SceneList LoadOrCreate() {
            var sceneListObject = AssetDatabase.LoadAssetAtPath<SceneList>(AssetPath);
            if (sceneListObject != null) {
                _asset = sceneListObject;
                return sceneListObject;
            }

            var dir = Path.GetDirectoryName(AssetPath);
            if (!Directory.Exists(dir) && !string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            sceneListObject = ScriptableObject.CreateInstance<SceneList>();
            sceneListObject.contents = new List<SceneList.SceneContent>();
            AssetDatabase.CreateAsset(sceneListObject, AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.LogWarning(I18N.Get("Debug.HierarchyExtension.ScheneListObject.NotFound", AssetPath));
            _asset = sceneListObject;
            return sceneListObject;
        }

        private static void Add(string path, bool isIgnored = false, bool isFavorite = false) {
            Initialize();
            var entry = new SceneList.SceneContent { path = path, isIgnored = isIgnored, isFavorite = isFavorite };
            _asset.contents.Add(entry);
            EditorUtility.SetDirty(_asset);
        }

        private static void Insert(int index, string path, bool isIgnored = false, bool isFavorite = false) {
            Initialize();
            var entry = new SceneList.SceneContent { path = path, isIgnored = isIgnored, isFavorite = isFavorite };
            index = Mathf.Clamp(index, 0, _asset.contents.Count);
            _asset.contents.Insert(index, entry);
            EditorUtility.SetDirty(_asset);
        }

        private static void Remove(int index) {
            Initialize();
            if (index < 0 || index >= _asset.contents.Count) return;
            _asset.contents.RemoveAt(index);
            EditorUtility.SetDirty(_asset);
        }

        private static void UpdateScene(int index, string path = null, bool? isIgnored = null,
            bool? isFavorite = null) {
            Initialize();
            if (index < 0 || index >= _asset.contents.Count) return;
            if (path != null) _asset.contents[index].path = path;
            if (isIgnored.HasValue) _asset.contents[index].isIgnored = isIgnored.Value;
            if (isFavorite.HasValue) _asset.contents[index].isFavorite = isFavorite.Value;
            EditorUtility.SetDirty(_asset);
        }

        public static void Move(int fromIndex, int toIndex) {
            Initialize();
            if (fromIndex < 0 || fromIndex >= _asset.Contents.Count) return;
            if (toIndex < 0 || toIndex >= _asset.Contents.Count) return;
            if (fromIndex == toIndex) return;
            var item = _asset.Contents[fromIndex];
            Remove(fromIndex);
            Insert(toIndex, item.path, item.isIgnored, item.isFavorite);
        }

        public static bool IsFavorite(string path) {
            Initialize();
            var scene = _asset?.Contents.FirstOrDefault(s => s.path == path);
            return scene?.isFavorite ?? false;
        }

        public static void ToggleFavorite(string path) {
            Initialize();
            var index = -1;
            for (var i = 0; i < _asset.Contents.Count; i++)
                if (_asset.Contents[i].path == path) {
                    index = i;
                    break;
                }

            if (index < 0) return;
            var currentValue = _asset.Contents[index].isFavorite;
            UpdateScene(index, isFavorite: !currentValue);
        }

        public static void MoveToTop(string path) {
            Initialize();
            var index = -1;
            for (var i = 0; i < _asset.Contents.Count; i++)
                if (_asset.Contents[i].path == path) {
                    index = i;
                    break;
                }

            if (index is < 0 or 0) return;

            var item = _asset.Contents[index];
            Remove(index);
            Insert(0, item.path, item.isIgnored, item.isFavorite);
        }

        public static void SceneListRegister() {
            var guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
            var currentPathList = guids.Select(AssetDatabase.GUIDToAssetPath).Where(p => !string.IsNullOrEmpty(p))
                .ToList();
            Initialize();

            for (var i = _asset.Contents.Count - 1; i >= 0; i--) {
                var path = _asset.Contents[i].path;
                if (currentPathList.All(p => p != path)) Remove(i);
            }

            foreach (var path in currentPathList.Where(path => _asset.Contents.All(s => s.path != path))) Add(path);
        }
    }
}