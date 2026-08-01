using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;

namespace Ee4v.UI.Tests
{
    public sealed class UiDesignTokenTests
    {
        private static readonly string[] TokenFileNames =
        {
            "ui-spacing-tokens.uss",
            "ui-shape-tokens.uss",
            "ui-typography-tokens.uss",
            "ui-size-tokens.uss"
        };

        private static readonly Regex TokenValueRegex = new Regex(
            @"--(?<name>ee4v-[a-z0-9-]+)\s*:\s*(?<value>-?\d+(?:\.\d+)?)(?:px)?\s*;",
            RegexOptions.Compiled);

        private static readonly IReadOnlyDictionary<string, float> ExpectedPrimitiveTokens =
            new Dictionary<string, float>
            {
                { "ee4v-space-none", UiSpacingTokens.None },
                { "ee4v-space-xxs", UiSpacingTokens.Xxs },
                { "ee4v-space-xs", UiSpacingTokens.Xs },
                { "ee4v-space-sm", UiSpacingTokens.Small },
                { "ee4v-space-md", UiSpacingTokens.Medium },
                { "ee4v-space-lg", UiSpacingTokens.Large },
                { "ee4v-space-xl", UiSpacingTokens.Xl },
                { "ee4v-space-2xl", UiSpacingTokens.Xxl },
                { "ee4v-space-3xl", UiSpacingTokens.Xxxl },
                { "ee4v-space-4xl", UiSpacingTokens.Huge },
                { "ee4v-space-negative-xxs", UiSpacingTokens.NegativeXxs },
                { "ee4v-space-negative-xs", UiSpacingTokens.NegativeXs },
                { "ee4v-border-hairline", UiBorderTokens.Hairline },
                { "ee4v-border-emphasis", UiBorderTokens.Emphasis },
                { "ee4v-border-strong", UiBorderTokens.Strong },
                { "ee4v-radius-xs", UiShapeTokens.RadiusXs },
                { "ee4v-radius-sm", UiShapeTokens.RadiusSmall },
                { "ee4v-radius-md", UiShapeTokens.RadiusMedium },
                { "ee4v-radius-lg", UiShapeTokens.RadiusLarge },
                { "ee4v-radius-xl", UiShapeTokens.RadiusXl },
                { "ee4v-radius-2xl", UiShapeTokens.RadiusXxl },
                { "ee4v-radius-3xl", UiShapeTokens.RadiusXxxl },
                { "ee4v-radius-pill", UiShapeTokens.RadiusPill },
                { "ee4v-font-size-micro", UiTypographyTokens.MicroFontSize },
                { "ee4v-font-size-caption", UiTypographyTokens.CaptionFontSize },
                { "ee4v-font-size-small", UiTypographyTokens.SmallFontSize },
                { "ee4v-font-size-body", UiTypographyTokens.BodyFontSize },
                { "ee4v-font-size-large-body", UiTypographyTokens.LargeBodyFontSize },
                { "ee4v-font-size-subtitle", UiTypographyTokens.SubtitleFontSize },
                { "ee4v-font-size-title", UiTypographyTokens.TitleFontSize },
                { "ee4v-font-size-heading", UiTypographyTokens.HeadingFontSize },
                { "ee4v-font-size-display", UiTypographyTokens.DisplayFontSize },
                { "ee4v-size-1", UiSizeTokens.Size1 },
                { "ee4v-size-2", UiSizeTokens.Size2 },
                { "ee4v-size-4", UiSizeTokens.Size4 },
                { "ee4v-size-8", UiSizeTokens.Size8 },
                { "ee4v-size-9", UiSizeTokens.Size9 },
                { "ee4v-size-10", UiSizeTokens.Size10 },
                { "ee4v-size-11", UiSizeTokens.Size11 },
                { "ee4v-size-12", UiSizeTokens.Size12 },
                { "ee4v-size-14", UiSizeTokens.Size14 },
                { "ee4v-size-16", UiSizeTokens.Size16 },
                { "ee4v-size-18", UiSizeTokens.Size18 },
                { "ee4v-size-20", UiSizeTokens.Size20 },
                { "ee4v-size-22", UiSizeTokens.Size22 },
                { "ee4v-size-24", UiSizeTokens.Size24 },
                { "ee4v-size-26", UiSizeTokens.Size26 },
                { "ee4v-size-28", UiSizeTokens.Size28 },
                { "ee4v-size-31", UiSizeTokens.Size31 }
            };

        private static readonly IReadOnlyDictionary<string, Regex> RawValueRules =
            new Dictionary<string, Regex>
            {
                {
                    "spacing",
                    CreatePropertyRegex(
                        @"padding(?:-(?:left|right|top|bottom))?|margin(?:-(?:left|right|top|bottom))?|gap|row-gap|column-gap",
                        @"0|-4px|-2px|2px|4px|6px|8px|10px|12px|16px|24px|28px")
                },
                {
                    "border",
                    CreatePropertyRegex(
                        @"border-(?:top|right|bottom|left)-width",
                        @"1px|2px|3px")
                },
                {
                    "radius",
                    CreatePropertyRegex(
                        @"border(?:-(?:top-left|top-right|bottom-left|bottom-right))?-radius",
                        @"2px|3px|4px|5px|6px|8px|12px")
                },
                {
                    "typography",
                    CreatePropertyRegex(
                        @"font-size",
                        @"7px|10px|11px|12px|13px|15px|16px|18px")
                },
                {
                    "size",
                    CreatePropertyRegex(
                        @"(?:min-|max-)?(?:width|height)",
                        @"1px|2px|4px|8px|9px|10px|11px|12px|14px|16px|18px|20px|22px|24px|26px|28px|31px")
                }
            };

        [Test]
        public void UssAndCSharpPrimitiveTokens_AreInSync()
        {
            var foundationPath = GetFoundationPath();
            Assert.That(foundationPath, Is.Not.Null.And.Not.Empty);

            var actualTokens = new Dictionary<string, float>(StringComparer.Ordinal);
            foreach (var fileName in TokenFileNames)
            {
                var contents = File.ReadAllText(Path.Combine(foundationPath, fileName));
                foreach (Match match in TokenValueRegex.Matches(contents))
                {
                    actualTokens[match.Groups["name"].Value] = float.Parse(
                        match.Groups["value"].Value,
                        System.Globalization.CultureInfo.InvariantCulture);
                }
            }

            var violations = ExpectedPrimitiveTokens
                .Where(expected =>
                    !actualTokens.TryGetValue(expected.Key, out var actual) ||
                    Math.Abs(actual - expected.Value) > 0.001f)
                .Select(expected => string.Format(
                    "{0}: expected {1}",
                    expected.Key,
                    expected.Value))
                .ToArray();

            Assert.That(violations, Is.Empty, string.Join("\n", violations));
        }

        [Test]
        public void UssFiles_UseSharedDesignTokens()
        {
            var editorRoot = GetEditorRootFullPath();
            Assert.That(editorRoot, Is.Not.Null.And.Not.Empty);

            var violations = new List<string>();
            var styleSheetPaths = Directory.GetFiles(editorRoot, "*.uss", SearchOption.AllDirectories)
                .Where(path => !TokenFileNames.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

            foreach (var styleSheetPath in styleSheetPaths)
            {
                var lines = File.ReadAllLines(styleSheetPath);
                for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    foreach (var rule in RawValueRules)
                    {
                        if (!rule.Value.IsMatch(lines[lineIndex]))
                        {
                            continue;
                        }

                        violations.Add(string.Format(
                            "{0}:{1} ({2})",
                            GetRelativePath(editorRoot, styleSheetPath),
                            lineIndex + 1,
                            rule.Key));
                    }
                }
            }

            Assert.That(violations, Is.Empty, string.Join("\n", violations));
        }

        [Test]
        public void CommonStyle_ImportsAggregateDesignTokens()
        {
            var editorRoot = GetEditorRootFullPath();
            var commonStylePath = Path.Combine(editorRoot, "UI", "Components", "common.uss");
            Assert.That(
                File.ReadAllText(commonStylePath),
                Does.Contain("@import url(\"../Foundation/ui-design-tokens.uss\");"));
        }

        private static Regex CreatePropertyRegex(string propertyPattern, string valuePattern)
        {
            return new Regex(
                @"^\s*(?:" + propertyPattern + @")\s*:[^;]*(?<![\w.-])(?:" + valuePattern + @")(?![\w.-])[^;]*;",
                RegexOptions.Compiled);
        }

        private static string GetFoundationPath()
        {
            var editorRoot = GetEditorRootFullPath();
            return string.IsNullOrEmpty(editorRoot)
                ? null
                : Path.Combine(editorRoot, "UI", "Foundation");
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
            var normalizedRoot = rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                                 Path.DirectorySeparatorChar;
            return path.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)
                ? path.Substring(normalizedRoot.Length).Replace('\\', '/')
                : path.Replace('\\', '/');
        }
    }
}
