using System.Collections.Generic;
using System.Linq;
using _4OF.ee4v.HierarchyExtension.Data;
using UnityEditor;
using UnityEngine.SceneManagement;

namespace _4OF.ee4v.HierarchyExtension.Service {
    public static class SceneListService {
        public static IReadOnlyList<string> SortedSceneList {
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

        public static int IndexOfPath(string path) {
            if (string.IsNullOrEmpty(path)) return -1;
            for (var i = 0; i < SceneList.instance.Contents.Count; i++)
                if (SceneList.instance.Contents[i].path == path)
                    return i;
            return -1;
        }

        public static void MoveToTop(string path) {
            var idx = IndexOfPath(path);
            switch (idx) {
                case > 0:
                    SceneList.instance.MoveScene(idx, 0);
                    return;
                case 0:
                    return;
                default:
                    SceneList.instance.InsertScene(0, path, false, false);
                    break;
            }
        }

        public static void SceneListRegister() {
            var guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
            var currentPathList = guids.Select(AssetDatabase.GUIDToAssetPath).Where(p => !string.IsNullOrEmpty(p))
                .ToList();

            for (var i = SceneList.instance.Contents.Count - 1; i >= 0; i--) {
                var path = SceneList.instance.Contents[i].path;
                if (currentPathList.All(p => p != path)) SceneList.instance.RemoveScene(i);
            }

            foreach (var path in currentPathList.Where(path => SceneList.instance.Contents.All(s => s.path != path)))
                SceneList.instance.Add(path, false, false);
        }
    }
}