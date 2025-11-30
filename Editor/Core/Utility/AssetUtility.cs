using System;
using System.IO;
using System.Linq;
using _4OF.ee4v.Core.Setting;
using UnityEditor;
using UnityEngine;

namespace _4OF.ee4v.Core.Utility {
    public static class AssetUtility {
        public static Font GetFont() {
            return EditorPrefsManager.Language switch {
                "ja-JP" => FindAndLoadFont("SourceHanSansJa-Regular"),
                "ko-KR" => FindAndLoadFont("SourceHanSansKo-Regular"),
                _       => null
            };
        }

        private static Font FindAndLoadFont(string fontName) {
            var guids = AssetDatabase.FindAssets($"{fontName} t:Font");

            return (from guid in guids
                select AssetDatabase.GUIDToAssetPath(guid)
                into path
                where Path.GetFileNameWithoutExtension(path).Equals(fontName, StringComparison.OrdinalIgnoreCase)
                select AssetDatabase.LoadAssetAtPath<Font>(path)).FirstOrDefault();
        }
    }
}