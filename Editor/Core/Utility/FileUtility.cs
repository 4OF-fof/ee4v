using System;
using System.IO;
using System.Linq;
using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.Core.Setting;
using UnityEditor;
using UnityEngine;

namespace _4OF.ee4v.Core.Utility {
    public static class AssetUtility {
        public static string NormalizePath(string path) {
            if (string.IsNullOrEmpty(path)) return path;
            var p = path.Trim().Replace('\\', '/');
            while (p.Length > 1 && p.EndsWith("/")) p = p[..^1];
            return p.ToLowerInvariant();
        }

        public static Font FindAndLoadFont(string fontName) {
            var guids = AssetDatabase.FindAssets($"{fontName} t:Font");

            return (from guid in guids select AssetDatabase.GUIDToAssetPath(guid) into path where Path.GetFileNameWithoutExtension(path).Equals(fontName, StringComparison.OrdinalIgnoreCase) select AssetDatabase.LoadAssetAtPath<Font>(path)).FirstOrDefault();
        }
    }
}