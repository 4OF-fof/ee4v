using UnityEngine;

namespace _4OF.ee4v.ProjectExtension.Data {
    public static class FolderStyleController {
        private static FolderStyleObject _asset;

        private static void Initialize() {
            if (_asset == null) _asset = FolderStyleObject.LoadOrCreate();
        }

        public static void Remove(string path) {
            Initialize();
            _asset.Remove(NormalizePath(path));
        }

        public static void UpdatePath(string oldPath, string newPath) {
            Initialize();
            _asset.UpdatePath(NormalizePath(oldPath), NormalizePath(newPath));
        }

        public static void UpdateOrAddColor(string path, Color color) {
            Initialize();
            var p = NormalizePath(path);
            if (_asset.GetStyle(p) == null)
                _asset.Add(p, color);
            else
                _asset.UpdateColor(p, color);
        }

        public static void UpdateOrAddIcon(string path, Texture icon) {
            Initialize();
            var p = NormalizePath(path);
            if (_asset.GetStyle(p) == null)
                _asset.Add(p, icon: icon);
            else
                _asset.UpdateIcon(p, icon);
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

        private static string NormalizePath(string path) {
            if (string.IsNullOrEmpty(path)) return path;
            var p = path.Trim().Replace('\\', '/');
            while (p.Length > 1 && p.EndsWith("/")) p = p[..^1];
            return p.ToLowerInvariant();
        }
    }
}