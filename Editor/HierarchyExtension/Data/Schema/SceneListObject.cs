using System;
using System.Collections.Generic;
using UnityEngine;

namespace _4OF.ee4v.HierarchyExtension.Data.Schema {
    public class SceneListObject : ScriptableObject {
        [SerializeField] internal List<SceneContent> sceneList = new();

        public IReadOnlyList<SceneContent> SceneList => sceneList;

        [Serializable]
        public class SceneContent {
            public string path;
            public bool isIgnored;
            public bool isFavorite;
        }
    }
}