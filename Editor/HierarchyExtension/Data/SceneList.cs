using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace _4OF.ee4v.HierarchyExtension.Data {
    [FilePath("ee4v/UserData/SceneList.asset", FilePathAttribute.Location.ProjectFolder)]
    public class SceneList : ScriptableSingleton<SceneList> {
        [SerializeField] private List<SceneContent> contents = new();
        public IReadOnlyList<SceneContent> Contents => contents;

        public void Add(string scenePath, bool isIgnored, bool isFavorite) {
            contents.Add(new SceneContent {
                path = scenePath,
                isIgnored = isIgnored,
                isFavorite = isFavorite
            });
            Save(true);
        }

        public void InsertScene(int index, string scenePath, bool isIgnored, bool isFavorite) {
            contents.Insert(index, new SceneContent {
                path = scenePath,
                isIgnored = isIgnored,
                isFavorite = isFavorite
            });
            Save(true);
        }

        public void MoveScene(int fromIndex, int toIndex) {
            if (fromIndex < 0 || fromIndex >= contents.Count) return;
            if (toIndex < 0 || toIndex >= contents.Count) return;
            if (fromIndex == toIndex) return;
            var item = contents[fromIndex];
            contents.RemoveAt(fromIndex);
            if (fromIndex < toIndex) toIndex--;
            contents.Insert(toIndex, item);
            Save(true);
        }

        public void RemoveScene(int index) {
            if (index < 0 || index >= contents.Count) return;
            contents.RemoveAt(index);
            Save(true);
        }

        public void UpdateScene(int index, string path = null, bool? isIgnored = null, bool? isFavorite = null) {
            if (index < 0 || index >= contents.Count) return;
            if (path != null) contents[index].path = path;
            if (isIgnored.HasValue) contents[index].isIgnored = isIgnored.Value;
            if (isFavorite.HasValue) contents[index].isFavorite = isFavorite.Value;
            Save(true);
        }

        [Serializable]
        public class SceneContent {
            public string path;
            public bool isIgnored;
            public bool isFavorite;
        }
    }
}