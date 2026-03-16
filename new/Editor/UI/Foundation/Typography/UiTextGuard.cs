using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;

namespace Ee4v.UI
{
    [InitializeOnLoad]
    internal static class UiTextGuard
    {
        private static readonly Regex NewLabelPattern = new Regex(@"\bnew\s+Label\s*\(", RegexOptions.Compiled);
        private static readonly Regex InheritLabelPattern = new Regex(@":\s*Label\b", RegexOptions.Compiled);

        static UiTextGuard()
        {
            EditorApplication.delayCall += Validate;
        }

        private static void Validate()
        {
            const string rootPath = "Assets/ee4v/Editor/UI";
            const string allowedPath = "Assets/ee4v/Editor/UI/Foundation/Typography";
            if (!AssetDatabase.IsValidFolder(rootPath))
            {
                return;
            }

            var violations = new List<string>();
            var guids = AssetDatabase.FindAssets("t:MonoScript", new[] { rootPath });
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrWhiteSpace(path) || path.StartsWith(allowedPath, StringComparison.Ordinal))
                {
                    continue;
                }

                var source = File.ReadAllText(Path.GetFullPath(path));
                if (NewLabelPattern.IsMatch(source) || InheritLabelPattern.IsMatch(source))
                {
                    violations.Add(path);
                }
            }

            if (violations.Count == 0)
            {
                return;
            }

            throw new InvalidOperationException(
                "UiTextGuard detected direct Label usage under Editor/UI:\n" + string.Join("\n", violations));
        }
    }
}
