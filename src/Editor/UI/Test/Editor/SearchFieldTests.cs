using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Ee4v.Testing.Contracts;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

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

        [Test]
        public void Placeholder_UsesSearchFieldLayoutAndImguiTypography()
        {
            var field = new SearchField(new SearchFieldState(
                placeholder: "Search files"));

            var placeholder = field.Q<UiTextElement>(
                className:
                "ee4v-ui-search-field__placeholder");

            Assert.That(placeholder, Is.Not.Null);
            Assert.That(
                placeholder.ClassListContains(
                    UiClassNames.InputPlaceholder),
                Is.True);
            Assert.That(
                placeholder.GetType().Name,
                Is.EqualTo("ImguiUiTextElement"));
        }

        [Test]
        public void IconStates_CanUseFluentPngTextures()
        {
            var field = new SearchField(
                new SearchFieldState(
                    searchIconState:
                    IconState.FromFluentIcon(
                        UiFluentIcon.Search,
                        UiSizeTokens.Size14),
                    clearIconState:
                    IconState.FromFluentIcon(
                        UiFluentIcon.Dismiss,
                        UiSizeTokens.Size10)));
            UiFluentIconResolver.TryResolve(
                UiFluentIcon.Search,
                out var searchTexture);
            UiFluentIconResolver.TryResolve(
                UiFluentIcon.Dismiss,
                out var clearTexture);
            var icons = field.Query<Icon>().ToList();

            Assert.That(icons.Count, Is.EqualTo(2));
            Assert.That(
                icons.Select(icon =>
                        icon.Q<Image>().image)
                    .ToArray(),
                Is.EqualTo(new[]
                {
                    searchTexture,
                    clearTexture
                }));
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
