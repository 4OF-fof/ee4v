using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace _4OF.ee4v.HierarchyExtension.Data {
    public static class SceneListController {
        private static SceneListObject _asset;

        public static List<string> ScenePathList {
            get {
                Initialize();
                return _asset?.SceneList.Where(s => !s.isIgnored).Select(s => s.path).ToList() ?? new List<string>();
            }
        }

        private static void Initialize() {
            if (_asset == null) _asset = SceneListObject.LoadOrCreate();
        }

        private static void Add(string path, bool isIgnored = false) {
            Initialize();
            _asset.Add(path, isIgnored);
        }

        private static void Remove(int index) {
            Initialize();
            _asset.Remove(index);
        }

        public static void Move(int fromIndex, int toIndex) {
            Initialize();
            if (fromIndex < 0 || fromIndex >= _asset.SceneList.Count) return;
            if (toIndex   < 0 || toIndex   >= _asset.SceneList.Count) return;
            if (fromIndex == toIndex) return;
            var item = _asset.SceneList[fromIndex];
            _asset.Remove(fromIndex);
            _asset.Insert(toIndex, item.path, item.isIgnored);
        }

        public static void UpdateScene(int index, string path, bool isIgnored) {
            Initialize();
            _asset.UpdateScene(index, path, isIgnored);
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