using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace _4OF.ee4v.ProjectExtension.Data {
    [FilePath("ee4v/UserData/FolderStyleList.asset", FilePathAttribute.Location.ProjectFolder)]
    public class FolderStyleList : ScriptableSingleton<FolderStyleList> {
        [SerializeField] private List<FolderStyle> contents = new();
        public IReadOnlyList<FolderStyle> Contents => contents;

        public void Add(string path, Color color, Texture icon) {
            contents.Add(new FolderStyle {
                path = path,
                color = color,
                icon = icon
            });
            Save(true);
        }

        public void Remove(int index) {
            if (index < 0 || index >= contents.Count) return;
            contents.RemoveAt(index);
            Save(true);
        }

        public void Update(int index, string path = null, Color? color = null, Texture icon = null, bool setIcon = false) {
            if (index < 0 || index >= contents.Count) return;
            if (path != null) contents[index].path = path;
            if (color.HasValue) contents[index].color = color.Value;
            if (setIcon) contents[index].icon = icon;
            Save(true);
        }

        [Serializable]
        public class FolderStyle {
            public string path;
            public Color color;
            public Texture icon;
        }
    }
}