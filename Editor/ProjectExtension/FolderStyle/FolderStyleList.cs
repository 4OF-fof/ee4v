using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace _4OF.ee4v.ProjectExtension.FolderStyle {
    [FilePath("ee4v/UserData/FolderStyleList.asset", FilePathAttribute.Location.ProjectFolder)]
    public class FolderStyleList : ScriptableSingleton<FolderStyleList> {
        [SerializeField] private List<FolderStyle> contents = new();
        public IReadOnlyList<FolderStyle> Contents => contents;

        public void AddFolderStyle(string guid, Color color, Texture icon, string assetUlid = null) {
            contents.Add(new FolderStyle {
                guid = guid,
                color = color,
                icon = icon,
                assetUlid = assetUlid
            });
            Save(true);
        }

        public void RemoveFolderStyle(int index) {
            if (index < 0 || index >= contents.Count) return;
            contents.RemoveAt(index);
            Save(true);
        }

        public void UpdateFolderStyle(int index, string guid = null, Color? color = null, Texture icon = null,
            bool setIcon = false, string assetUlid = null) {
            if (index < 0 || index >= contents.Count) return;

            var style = contents[index];
            if (guid != null) style.guid = guid;
            if (color.HasValue) style.color = color.Value;
            if (setIcon) style.icon = icon;
            if (assetUlid != null) style.assetUlid = assetUlid;

            contents[index] = style;
            Save(true);
        }

        [Serializable]
        public class FolderStyle {
            public string guid;
            public Color color;
            public Texture icon;
            public string assetUlid;
        }
    }
}