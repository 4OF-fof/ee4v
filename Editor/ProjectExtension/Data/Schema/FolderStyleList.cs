using System;
using System.Collections.Generic;
using UnityEngine;

namespace _4OF.ee4v.ProjectExtension.Data.Schema {
    [CreateAssetMenu(fileName = "FolderStyle")]
    public class FolderStyleList : ScriptableObject {
        [SerializeField] internal List<FolderStyle> contents = new();

        public IReadOnlyList<FolderStyle> Contents => contents;

        [Serializable]
        public class FolderStyle {
            public string path;
            public Color color;
            public Texture icon;
        }
    }
}