using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine.SceneManagement;

namespace _4OF.ee4v.HierarchyExtension.Data {
    public static class SceneListController {
        public static List<string> ScenePathList {
            get {
                var scenes = SceneList.instance.Contents.Where(s => !s.isIgnored).ToList();

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
        
        private static void Add(string path, bool isIgnored = false, bool isFavorite = false) {
            SceneList.instance.Add(path, isIgnored, isFavorite);
        }

        private static void Insert(int index, string path, bool isIgnored = false, bool isFavorite = false) {
            SceneList.instance.Insert(index, path, isIgnored, isFavorite);
        }

        private static void Remove(int index) {
            SceneList.instance.Remove(index);
        }

        private static void UpdateScene(int index, string path = null, bool? isIgnored = null,
            bool? isFavorite = null) {
            SceneList.instance.Update(index, path, isIgnored, isFavorite);
        }

        public static void Move(int fromIndex, int toIndex) {
            SceneList.instance.Move(fromIndex, toIndex);
        }

        public static bool IsFavorite(string path) {
            var scene = SceneList.instance.Contents.FirstOrDefault(s => s.path == path);
            return scene?.isFavorite ?? false;
        }

        public static void ToggleFavorite(string path) {
            var index = -1;
            for (var i = 0; i < SceneList.instance.Contents.Count; i++)
                if (SceneList.instance.Contents[i].path == path) {
                    index = i;
                    break;
                }

            if (index < 0) return;
            var currentValue = SceneList.instance.Contents[index].isFavorite;
            UpdateScene(index, isFavorite: !currentValue);
        }

        public static void MoveToTop(string path) {
            var index = -1;
            for (var i = 0; i < SceneList.instance.Contents.Count; i++)
                if (SceneList.instance.Contents[i].path == path) {
                    index = i;
                    break;
                }

            if (index is < 0 or 0) return;

            var item = SceneList.instance.Contents[index];
            Remove(index);
            Insert(0, item.path, item.isIgnored, item.isFavorite);
        }

        public static void SceneListRegister() {
            var guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
            var currentPathList = guids.Select(AssetDatabase.GUIDToAssetPath).Where(p => !string.IsNullOrEmpty(p))
                .ToList();
            

            for (var i = SceneList.instance.Contents.Count - 1; i >= 0; i--) {
                var path = SceneList.instance.Contents[i].path;
                if (currentPathList.All(p => p != path)) Remove(i);
            }

            foreach (var path in currentPathList.Where(path => SceneList.instance.Contents.All(s => s.path != path))) Add(path);
        }
    }
}