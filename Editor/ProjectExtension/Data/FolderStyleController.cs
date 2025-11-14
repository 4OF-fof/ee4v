using System.Linq;
using _4OF.ee4v.ProjectExtension.Service;
using UnityEditor;
using UnityEngine;

namespace _4OF.ee4v.ProjectExtension.Data {
    public static class FolderStyleController {
        private static void Add(string path, Color? color = null, Texture icon = null) {
            path = FolderStyleService.NormalizePath(path);
            var idx = FolderStyleService.IndexOfPath(path);
            if (idx != -1) return;
            color ??= Color.clear;
            FolderStyleList.instance.Add(path, color.Value, icon);
        }

        public static void Remove(string path) {
            path = FolderStyleService.NormalizePath(path);
            var index = FolderStyleService.IndexOfPath(path);
            if (index < 0) return;
            FolderStyleList.instance.Remove(index);
            EditorUtility.SetDirty(FolderStyleList.instance);
        }

        public static void UpdatePath(string oldPath, string newPath) {
            oldPath = FolderStyleService.NormalizePath(oldPath);
            newPath = FolderStyleService.NormalizePath(newPath);
            var index = FolderStyleService.IndexOfPath(oldPath);
            if (index < 0) return;
            FolderStyleList.instance.Update(index, newPath);
            EditorUtility.SetDirty(FolderStyleList.instance);
        }

        private static void UpdateColor(string path, Color color) {
            path = FolderStyleService.NormalizePath(path);
            var index = FolderStyleService.IndexOfPath(path);
            if (index < 0) return;
            FolderStyleList.instance.Update(index, color: color);
            EditorUtility.SetDirty(FolderStyleList.instance);
        }

        private static void UpdateIcon(string path, Texture icon) {
            path = FolderStyleService.NormalizePath(path);
            var index = FolderStyleService.IndexOfPath(path);
            if (index < 0) return;
            FolderStyleList.instance.Update(index, icon: icon);
            EditorUtility.SetDirty(FolderStyleList.instance);
        }

        public static void UpdateOrAddColor(string path, Color color) {
            var p = FolderStyleService.NormalizePath(path);
            var idx = FolderStyleService.IndexOfPath(p);
            if (idx == -1)
                Add(p, color);
            else
                UpdateColor(p, color);
        }

        public static void UpdateOrAddIcon(string path, Texture icon) {
            var p = FolderStyleService.NormalizePath(path);
            var idx = FolderStyleService.IndexOfPath(p);
            if (idx == -1)
                Add(p, icon: icon);
            else
                UpdateIcon(p, icon);
        }

        public static Color GetColor(string path) {
            var style = FolderStyleList.instance.Contents.FirstOrDefault(s => s.path == FolderStyleService.NormalizePath(path));
            return style?.color ?? Color.clear;
        }

        public static Texture GetIcon(string path) {
            var style = FolderStyleList.instance.Contents.FirstOrDefault(s => s.path == FolderStyleService.NormalizePath(path));
            return style?.icon;
        }
    }
}