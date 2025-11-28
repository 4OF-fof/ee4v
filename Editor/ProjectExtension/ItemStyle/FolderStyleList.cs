using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace _4OF.ee4v.ProjectExtension.ItemStyle {
    [FilePath("ee4v/UserData/FolderStyleList.asset", FilePathAttribute.Location.ProjectFolder)]
    public class FolderStyleList : ScriptableSingleton<FolderStyleList> {
        [SerializeField] private List<FolderStyle> contents = new();
        public IReadOnlyList<FolderStyle> Contents => contents;

        public void AddFolderStyle(string guid, Color color, Texture icon) {
            contents.Add(new FolderStyle {
                guid = guid,
                color = color,
                icon = icon
            });
            Save(true);
        }

        public void RemoveFolderStyle(int index) {
            if (index < 0 || index >= contents.Count) return;
            contents.RemoveAt(index);
            Save(true);
        }

        public void UpdateFolderStyle(int index, string guid = null, Color? color = null, Texture icon = null,
            bool setIcon = false) {
            if (index < 0 || index >= contents.Count) return;
            if (guid != null) contents[index].guid = guid;
            if (color.HasValue) contents[index].color = color.Value;
            if (setIcon) contents[index].icon = icon;
            Save(true);
        }

        [Serializable]
        public class FolderStyle {
            public string guid;
            public Color color;
            public Texture icon;
        }
    }
}