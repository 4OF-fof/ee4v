using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine.SceneManagement;

namespace _4OF.ee4v.HierarchyExtension.Data {
    public static class SceneListController {
        private static SceneListObject _asset;

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

        private static void Initialize() {
            if (_asset == null) _asset = SceneListObject.LoadOrCreate();
        }

        private static void Add(string path, bool isIgnored = false) {
            Initialize();
            _asset.Add(path, isIgnored);
            EditorUtility.SetDirty(_asset);
        }

        private static void Remove(int index) {
            Initialize();
            _asset.Remove(index);
            EditorUtility.SetDirty(_asset);
        }

        public static void Move(int fromIndex, int toIndex) {
            Initialize();
            if (fromIndex < 0 || fromIndex >= _asset.SceneList.Count) return;
            if (toIndex < 0 || toIndex >= _asset.SceneList.Count) return;
            if (fromIndex == toIndex) return;
            var item = _asset.SceneList[fromIndex];
            _asset.Remove(fromIndex);
            _asset.Insert(toIndex, item.path, item.isIgnored, item.isFavorite);
            EditorUtility.SetDirty(_asset);
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
            _asset.UpdateScene(index, isFavorite: !currentValue);
            EditorUtility.SetDirty(_asset);
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
            _asset.Remove(index);
            _asset.Insert(0, item.path, item.isIgnored, item.isFavorite);
            EditorUtility.SetDirty(_asset);
        }

        public static void SceneListRegister() {
            var guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
            var currentPathList = guids.Select(AssetDatabase.GUIDToAssetPath).Where(p => !string.IsNullOrEmpty(p))
                .ToList();
            var sceneList = SceneListObject.LoadOrCreate();

            for (var i = sceneList.SceneList.Count - 1; i >= 0; i--) {
                var path = sceneList.SceneList[i].path;
                if (currentPathList.All(p => p != path)) Remove(i);
            }

            foreach (var path in currentPathList.Where(path => sceneList.SceneList.All(s => s.path != path))) Add(path);
        }
    }
}