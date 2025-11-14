using System.Collections.Generic;
using System.IO;
using System.Linq;
using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.ProjectExtension.Data.Schema;
using UnityEditor;
using UnityEngine;

namespace _4OF.ee4v.ProjectExtension.Data {
    public static class FolderStyleController {
        private const string AssetPath = "Assets/4OF/ee4v/UserData/FolderStyleObject.asset";
        private static FolderStyleList _asset;

        public static FolderStyleList GetInstance() {
            if (_asset == null) _asset = LoadOrCreate();
            return _asset;
        }

        private static void Initialize() {
            if (_asset == null) _asset = LoadOrCreate();
        }

        private static FolderStyleList LoadOrCreate() {
            var folderStyleObject = AssetDatabase.LoadAssetAtPath<FolderStyleList>(AssetPath);
            if (folderStyleObject != null) {
                _asset = folderStyleObject;
                return folderStyleObject;
            }

            var dir = Path.GetDirectoryName(AssetPath);
            if (!Directory.Exists(dir) && !string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            folderStyleObject = ScriptableObject.CreateInstance<FolderStyleList>();
            folderStyleObject.contents = new List<FolderStyleList.FolderStyle>();
            AssetDatabase.CreateAsset(folderStyleObject, AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.LogWarning(I18N.Get("Debug.ProjectExtension.NotFoundFolderStyleObject", AssetPath));
            _asset = folderStyleObject;
            return folderStyleObject;
        }

        private static void Add(string path, Color? color = null, Texture icon = null) {
            Initialize();
            if (_asset.contents.Any(style => style.path == path)) return;
            color ??= Color.clear;
            var entry = new FolderStyleList.FolderStyle { path = path, color = color.Value, icon = icon };
            _asset.contents.Add(entry);
            EditorUtility.SetDirty(_asset);
        }

        public static void Remove(string path) {
            Initialize();
            path = NormalizePath(path);
            var index = _asset.contents.FindIndex(style => style.path == path);
            if (index < 0 || index >= _asset.contents.Count) return;
            _asset.contents.RemoveAt(index);
            EditorUtility.SetDirty(_asset);
        }

        public static void UpdatePath(string oldPath, string newPath) {
            Initialize();
            oldPath = NormalizePath(oldPath);
            newPath = NormalizePath(newPath);
            var index = _asset.contents.FindIndex(style => style.path == oldPath);
            if (index < 0 || index >= _asset.contents.Count) return;
            _asset.contents[index].path = newPath;
            EditorUtility.SetDirty(_asset);
        }

        private static void UpdateColor(string path, Color color) {
            Initialize();
            var index = _asset.contents.FindIndex(style => style.path == path);
            if (index < 0 || index >= _asset.contents.Count) return;
            _asset.contents[index].color = color;
            EditorUtility.SetDirty(_asset);
        }

        private static void UpdateIcon(string path, Texture icon) {
            Initialize();
            var index = _asset.contents.FindIndex(style => style.path == path);
            if (index < 0 || index >= _asset.contents.Count) return;
            _asset.contents[index].icon = icon;
            EditorUtility.SetDirty(_asset);
        }

        private static FolderStyleList.FolderStyle GetStyle(string path) {
            Initialize();
            return _asset.contents.FirstOrDefault(style => style.path == path);
        }

        public static void UpdateOrAddColor(string path, Color color) {
            Initialize();
            var p = NormalizePath(path);
            if (GetStyle(p) == null)
                Add(p, color);
            else
                UpdateColor(p, color);
        }

        public static void UpdateOrAddIcon(string path, Texture icon) {
            Initialize();
            var p = NormalizePath(path);
            if (GetStyle(p) == null)
                Add(p, icon: icon);
            else
                UpdateIcon(p, icon);
        }

        public static Color GetColor(string path) {
            Initialize();
            var style = GetStyle(NormalizePath(path));
            return style?.color ?? Color.clear;
        }

        public static Texture GetIcon(string path) {
            Initialize();
            var style = GetStyle(NormalizePath(path));
            return style?.icon;
        }

        private static string NormalizePath(string path) {
            if (string.IsNullOrEmpty(path)) return path;
            var p = path.Trim().Replace('\\', '/');
            while (p.Length > 1 && p.EndsWith("/")) p = p[..^1];
            return p.ToLowerInvariant();
        }
    }
}