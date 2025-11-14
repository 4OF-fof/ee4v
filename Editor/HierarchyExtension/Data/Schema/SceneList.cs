using System;
using System.Collections.Generic;
using UnityEngine;

namespace _4OF.ee4v.HierarchyExtension.Data.Schema {
    public class SceneList : ScriptableObject {
        [SerializeField] internal List<SceneContent> contents = new();

        public IReadOnlyList<SceneContent> Contents => contents;

        [Serializable]
        public class SceneContent {
            public string path;
            public bool isIgnored;
            public bool isFavorite;
        }
    }
}