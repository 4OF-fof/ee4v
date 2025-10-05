using UnityEditor;
using UnityEngine;

namespace _4OF.ee4v.ProjectExtension.Data {
    public static class FolderStyleController {
        private static FolderStyleObject _asset;

        public static void Initialize() {
            if (_asset == null) _asset = FolderStyleObject.LoadOrCreate();
        }

        public static void Remove(string path) {
            Initialize();
            _asset.Remove(NormalizePath(path));
            Save();
        }

        public static void UpdatePath(string oldPath, string newPath) {
            Initialize();
            _asset.UpdatePath(NormalizePath(oldPath), NormalizePath(newPath));
            Save();
        }

        public static void UpdateOrAddColor(string path, Color color) {
            Initialize();
            var p = NormalizePath(path);
            if (_asset.GetStyle(p) == null)
                _asset.Add(p, color);
            else
                _asset.UpdateColor(p, color);
            Save();
        }

        public static void UpdateOrAddIcon(string path, Texture icon) {
            Initialize();
            var p = NormalizePath(path);
            if (_asset.GetStyle(p) == null)
                _asset.Add(p, icon: icon);
            else
                _asset.UpdateIcon(p, icon);
            Save();
        }

        public static Color GetColor(string path) {
            Initialize();
            var style = _asset.GetStyle(NormalizePath(path));
            return style?.color ?? Color.clear;
        }

        public static Texture GetIcon(string path) {
            Initialize();
            var style = _asset.GetStyle(NormalizePath(path));
            return style?.icon;
        }

        private static void Save() {
            if (_asset == null) return;
            EditorUtility.SetDirty(_asset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static string NormalizePath(string path) {
            if (string.IsNullOrEmpty(path)) return path;
            var p = path.Trim().Replace('\\', '/');
            while (p.Length > 1 && p.EndsWith("/")) p = p[..^1];
            return p.ToLowerInvariant();
        }
    }
}