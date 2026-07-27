using System;
using System.IO;
using System.Text.RegularExpressions;
using Ee4v.Testing.Contracts;
using NUnit.Framework;
using UnityEditor;

namespace Ee4v.UI.Tests
{
    public sealed class SearchFieldTests
    {
        [Test]
        [FeatureTestCase(
            "狭い SearchField で placeholder を clip する",
            "長い placeholder が入力領域から隣接 control へはみ出さないよう、汎用 component の host と text を clip することを確認します。",
            order: 230,
            category: FeatureTestCategory.Ui)]
        public void Placeholder_UsesClippingAtNarrowWidths()
        {
            var stylePath = FindSearchFieldStylePath();
            Assert.That(stylePath, Is.Not.Null.And.Not.Empty);
            var styleSheet = File.ReadAllText(
                Path.GetFullPath(stylePath));

            AssertRuleClips(
                styleSheet,
                ".ee4v-ui-search-field__input-host");
            AssertRuleClips(
                styleSheet,
                ".ee4v-ui-search-field__placeholder");
            AssertRuleClips(
                styleSheet,
                ".ee4v-ui-search-field__placeholder Label");
        }

        private static void AssertRuleClips(
            string styleSheet,
            string selector)
        {
            Assert.That(
                Regex.IsMatch(
                    styleSheet,
                    Regex.Escape(selector) +
                    @"\s*\{[^}]*\boverflow\s*:\s*hidden\s*;",
                    RegexOptions.Singleline),
                Is.True,
                selector);
        }

        private static string FindSearchFieldStylePath()
        {
            var guids = AssetDatabase.FindAssets(
                "search-field t:StyleSheet");
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (path.EndsWith(
                        "Editor/UI/Components/Inputs/SearchField/search-field.uss",
                        StringComparison.Ordinal))
                {
                    return path;
                }
            }

            return null;
        }
    }
}
