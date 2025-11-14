using System;
using System.Collections.Generic;
using UnityEngine;

namespace _4OF.ee4v.ProjectExtension.Data.Schema {
    [CreateAssetMenu(fileName = "FolderStyle")]
    public class FolderStyleObject : ScriptableObject {
        [SerializeField] internal List<FolderStyle> styledFolderList = new();

        public IReadOnlyList<FolderStyle> StyledFolderList => styledFolderList;

        [Serializable]
        public class FolderStyle {
            public string path;
            public Color color;
            public Texture icon;
        }
    }
}