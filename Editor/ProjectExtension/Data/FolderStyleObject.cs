using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace _4OF.ee4v.ProjectExtension.Data {
    [CreateAssetMenu(fileName = "FolderStyle")]
    public class FolderStyleObject : ScriptableObject {
        [SerializeField] private List<FolderStyle> styledFolderList = new();

        public IReadOnlyList<FolderStyle> StyledFolderList => styledFolderList;

        public void Add(string path, Color? color = null, Texture icon = null) {
            if (styledFolderList.Any(style => style.path == path)) return;
            color ??= Color.clear;
            var entry = new FolderStyle { path = path, color = color.Value, icon = icon };
            styledFolderList.Add(entry);
        }

        public void Remove(string path) {
            var index = styledFolderList.IndexOf(styledFolderList.FirstOrDefault(style => style.path == path));
            if (index < 0 || index >= styledFolderList.Count) return;
            styledFolderList.RemoveAt(index);
        }

        public void UpdatePath(string oldPath, string newPath) {
            var index = styledFolderList.IndexOf(styledFolderList.FirstOrDefault(style => style.path == oldPath));
            if (index < 0 || index >= styledFolderList.Count) return;
            styledFolderList[index].path = newPath;
        }

        public void UpdateColor(string path, Color color) {
            var index = styledFolderList.IndexOf(styledFolderList.FirstOrDefault(style => style.path == path));
            if (index < 0 || index >= styledFolderList.Count) return;
            styledFolderList[index].color = color;
        }

        public void UpdateIcon(string path, Texture icon) {
            var index = styledFolderList.IndexOf(styledFolderList.FirstOrDefault(style => style.path == path));
            if (index < 0 || index >= styledFolderList.Count) return;
            styledFolderList[index].icon = icon;
        }

        public FolderStyle GetStyle(string path) {
            return styledFolderList.FirstOrDefault(style => style.path == path);
        }

        public static FolderStyleObject LoadOrCreate() {
            var temp = CreateInstance<FolderStyleObject>();
            var scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(temp));
            DestroyImmediate(temp);
            var path = scriptPath.Replace("ProjectExtension/Data/FolderStyleObject.cs",
                "UserData/FolderStyleObject.asset");
            var folderStyleObject = AssetDatabase.LoadAssetAtPath<FolderStyleObject>(path);
            if (folderStyleObject != null) return folderStyleObject;

            var dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir) && !string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            folderStyleObject = CreateInstance<FolderStyleObject>();
            folderStyleObject.styledFolderList = new List<FolderStyle>();
            AssetDatabase.CreateAsset(folderStyleObject, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.LogWarning($"FolderStyleObject not found at {path}. Creating new one.");
            return folderStyleObject;
        }

        [Serializable]
        public class FolderStyle {
            public string path;
            public Color color;
            public Texture icon;
        }
    }
}