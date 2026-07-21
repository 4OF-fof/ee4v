using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;

namespace Ee4v.UI.Tests
{
    public sealed class UiColorTokenTests
    {
        private const string ColorTokenFileName = "ui-color-tokens.uss";
        private static readonly Regex RawColorRegex = new Regex(
            @"#[0-9a-fA-F]{3,8}\b|rgba?\s*\(|:\s*transparent\s*;",
            RegexOptions.Compiled);

        [Test]
        public void UssFiles_UseSharedColorTokens()
        {
            var editorRoot = GetEditorRootFullPath();
            Assert.That(editorRoot, Is.Not.Null.And.Not.Empty);

            var violations = new List<string>();
            var styleSheetPaths = Directory.GetFiles(editorRoot, "*.uss", SearchOption.AllDirectories)
                .Where(path => !string.Equals(Path.GetFileName(path), ColorTokenFileName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

            foreach (var styleSheetPath in styleSheetPaths)
            {
                var lines = File.ReadAllLines(styleSheetPath);
                for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    if (RawColorRegex.IsMatch(lines[lineIndex]))
                    {
                        violations.Add(string.Format(
                            "{0}:{1}",
                            GetRelativePath(editorRoot, styleSheetPath),
                            lineIndex + 1));
                    }
                }
            }

            Assert.That(violations, Is.Empty, string.Join("\n", violations));
        }

        private static string GetEditorRootFullPath()
        {
            var anchorAssetPath = AssetDatabase.FindAssets("Ee4vPackageAnchor")
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(path => path.EndsWith("Editor/Core/Internal/Ee4vPackageAnchor.cs", StringComparison.Ordinal));
            if (string.IsNullOrEmpty(anchorAssetPath))
            {
                return null;
            }

            var packageRootAssetPath = Path.GetDirectoryName(
                Path.GetDirectoryName(
                    Path.GetDirectoryName(
                        Path.GetDirectoryName(anchorAssetPath))));
            return string.IsNullOrEmpty(packageRootAssetPath)
                ? null
                : Path.Combine(Path.GetFullPath(packageRootAssetPath), "Editor");
        }

        private static string GetRelativePath(string rootPath, string path)
        {
            var normalizedRoot = rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return path.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)
                ? path.Substring(normalizedRoot.Length).Replace('\\', '/')
                : path.Replace('\\', '/');
        }
    }
}
