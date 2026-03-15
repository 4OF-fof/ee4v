using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace _4OF.ee4v.ProjectExtension.StyledFolder {
    [FilePath("ee4v/UserData/FolderStyleList.asset", FilePathAttribute.Location.ProjectFolder)]
    public class FolderStyleList : ScriptableSingleton<FolderStyleList> {
        [SerializeField] private List<FolderStyle> contents = new();
        public IReadOnlyList<FolderStyle> Contents => contents;

        public FolderStyle GetStyle(string guid) {
            return contents.FirstOrDefault(s => s.guid == guid);
        }

        public void SetStyle(string guid, Color? color = null, Texture icon = null, bool setIcon = false,
            string assetUlid = null) {
            var index = contents.FindIndex(s => s.guid == guid);

            if (index == -1) {
                contents.Add(new FolderStyle {
                    guid = guid,
                    color = color ?? Color.clear,
                    icon = icon,
                    assetUlid = assetUlid
                });
            }
            else {
                var style = contents[index];
                if (color.HasValue) style.color = color.Value;
                if (setIcon) style.icon = icon;
                if (assetUlid != null) style.assetUlid = assetUlid;
                contents[index] = style;
            }

            Save(true);
        }

        public void RemoveStyle(string guid) {
            var index = contents.FindIndex(s => s.guid == guid);
            if (index < 0) return;
            contents.RemoveAt(index);
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