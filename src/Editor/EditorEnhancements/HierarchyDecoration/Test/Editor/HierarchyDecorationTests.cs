using Ee4v.Testing.Contracts;
using NUnit.Framework;

namespace Ee4v.HierarchyDecoration.Tests
{
    public sealed class HierarchyDecorationTests
    {
        [TestCase("---", 1, 0, true)]
        [TestCase("--- ", 1, 0, false)]
        [TestCase("Section ---", 1, 0, false)]
        [TestCase("---", 2, 0, false)]
        [TestCase("---", 1, 1, false)]
        [FeatureTestCase(
            "Empty GameObject の --- だけを区切り線にする",
            "名前の完全一致に加え、Transform以外のcomponentと子を持たない場合だけ区切り線として扱うことを確認します。",
            order: 20)]
        public void Rules_RecognizeOnlyEmptyExactMarker(
            string objectName,
            int componentCount,
            int childCount,
            bool expected)
        {
            Assert.That(
                HierarchyDecorationRules.IsSeparator(
                    objectName,
                    componentCount,
                    childCount),
                Is.EqualTo(expected));
        }

        [TestCase("--- (1)", 1, 0, true, "---")]
        [TestCase("--- (20)", 1, 0, true, "---")]
        [TestCase("--- (0)", 1, 0, false, "--- (0)")]
        [TestCase("--- (-1)", 1, 0, false, "--- (-1)")]
        [TestCase("--- (name)", 1, 0, false, "--- (name)")]
        [TestCase("--- (1) ", 1, 0, false, "--- (1) ")]
        [TestCase("--- (1)", 2, 0, false, "--- (1)")]
        [TestCase("--- (1)", 1, 1, false, "--- (1)")]
        [FeatureTestCase(
            "複製した区切り線の連番suffixを除去する",
            "Empty条件を保った区切り線だけを---へ戻し、通常objectの名前は変更しないことを確認します。",
            order: 30)]
        public void Rules_NormalizeOnlyDuplicatedEmptySeparator(
            string objectName,
            int componentCount,
            int childCount,
            bool expected,
            string expectedName)
        {
            var normalized =
                HierarchyDecorationRules
                    .TryNormalizeSeparatorName(
                        objectName,
                        componentCount,
                        childCount,
                        out var normalizedName);

            Assert.That(normalized, Is.EqualTo(expected));
            Assert.That(normalizedName, Is.EqualTo(expectedName));
        }
    }
}
