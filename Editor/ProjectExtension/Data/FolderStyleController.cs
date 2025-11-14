using System.Linq;
using UnityEditor;
using UnityEngine;

namespace _4OF.ee4v.ProjectExtension.Data {
    public static class FolderStyleController {
        private static void Add(string path, Color? color = null, Texture icon = null) {
            path = NormalizePath(path);
            if (FolderStyleList.instance.Contents.Any(style => style.path == path)) return;
            color ??= Color.clear;
            FolderStyleList.instance.Add(path, color.Value, icon);
        }

        public static void Remove(string path) {
            path = NormalizePath(path);
            var index = FolderStyleList.instance.Contents.ToList().FindIndex(style => style.path == path);
            if (index < 0) return;
            FolderStyleList.instance.Remove(index);
            EditorUtility.SetDirty(FolderStyleList.instance);
        }

        public static void UpdatePath(string oldPath, string newPath) {
            oldPath = NormalizePath(oldPath);
            newPath = NormalizePath(newPath);
            var index = FolderStyleList.instance.Contents.ToList().FindIndex(style => style.path == oldPath);
            if (index < 0) return;
            FolderStyleList.instance.Update(index, newPath);
            EditorUtility.SetDirty(FolderStyleList.instance);
        }

        private static void UpdateColor(string path, Color color) {
            var index = FolderStyleList.instance.Contents.ToList().FindIndex(style => style.path == path);
            if (index < 0) return;
            FolderStyleList.instance.Update(index, color: color);
            EditorUtility.SetDirty(FolderStyleList.instance);
        }

        private static void UpdateIcon(string path, Texture icon) {
            var index = FolderStyleList.instance.Contents.ToList().FindIndex(style => style.path == path);
            if (index < 0) return;
            FolderStyleList.instance.Update(index, icon: icon);
            EditorUtility.SetDirty(FolderStyleList.instance);
        }

        public static void UpdateOrAddColor(string path, Color color) {
            var p = NormalizePath(path);
            var idx = FolderStyleList.instance.Contents.ToList().FindIndex(s => s.path == p);
            if (idx == -1)
                Add(p, color);
            else
                UpdateColor(p, color);
        }

        public static void UpdateOrAddIcon(string path, Texture icon) {
            var p = NormalizePath(path);
            var idx = FolderStyleList.instance.Contents.ToList().FindIndex(s => s.path == p);
            if (idx == -1)
                Add(p, icon: icon);
            else
                UpdateIcon(p, icon);
        }

        public static Color GetColor(string path) {
            var style = FolderStyleList.instance.Contents.FirstOrDefault(s => s.path == NormalizePath(path));
            return style?.color ?? Color.clear;
        }

        public static Texture GetIcon(string path) {
            var style = FolderStyleList.instance.Contents.FirstOrDefault(s => s.path == NormalizePath(path));
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