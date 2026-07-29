using Ee4v.Testing.Contracts;
using NUnit.Framework;

[assembly: FeatureTestSuite(
    "AssetManager Domain",
    "AssetManager",
    "Ee4v.AssetManager.Domain.Tests.Editor",
    "AssetManager の技術非依存な invariant と policy を確認します。",
    order: 301)]

namespace Ee4v.AssetManager.Domain.Tests
{
    public sealed class ImportTargetPathPolicyTests
    {
        [Test]
        [FeatureTestCase(
            "Import Target path を正規化する",
            "slash、前後区切り、大小文字違いの重複を domain policy が正規化することを確認します。",
            order: 1)]
        public void Normalize_CanonicalizesAndRemovesDuplicates()
        {
            var result = ImportTargetPathPolicy.Normalize(
                new[]
                {
                    "\\Packages\\avatar.unitypackage/",
                    "packages/avatar.unitypackage",
                    "Textures/albedo.png"
                });

            Assert.That(
                result,
                Is.EqualTo(new[]
                {
                    "Packages/avatar.unitypackage",
                    "Textures/albedo.png"
                }));
        }

        [TestCase("")]
        [TestCase("/")]
        [TestCase(".")]
        [TestCase("..")]
        [TestCase("Packages/../outside.unitypackage")]
        [FeatureTestCase(
            "Import Target は file 配下だけを許可する",
            "file root、current segment、parent traversal を domain policy が拒否することを確認します。",
            order: 2)]
        public void Normalize_RejectsPathsOutsideAFileChild(string relativePath)
        {
            Assert.Throws<ImportTargetPathRuleException>(
                () => ImportTargetPathPolicy.Normalize(new[] { relativePath }));
        }
    }

    public sealed class CatalogCommandPolicyTests
    {
        [Test]
        [FeatureTestCase(
            "自己依存を永続化前に拒否する",
            "file dependency の source と target が同じ identity の場合に domain policy が拒否することを確認します。",
            order: 3)]
        public void EnsureNoSelfDependency_RejectsSameIdentity()
        {
            Assert.Throws<CatalogRuleException>(
                () => CatalogCommandPolicy.EnsureNoSelfDependency(
                    "file-1",
                    new[] { "file-2", "file-1" }));
        }

        [Test]
        [FeatureTestCase(
            "Smart Collection condition の入力を検証する",
            "condition がない場合と query text が空の場合を domain policy が区別して拒否することを確認します。",
            order: 4)]
        public void EnsureSmartConditions_RejectsMissingConditionAndQuery()
        {
            var missing = Assert.Throws<CatalogRuleException>(
                () => CatalogCommandPolicy.EnsureSmartConditions(
                    System.Array.Empty<string>(),
                    false));
            var emptyQuery = Assert.Throws<CatalogRuleException>(
                () => CatalogCommandPolicy.EnsureSmartConditions(
                    new[] { string.Empty },
                    false));

            Assert.That(missing.Error, Is.EqualTo(CatalogRuleError.SmartConditionRequired));
            Assert.That(
                emptyQuery.Error,
                Is.EqualTo(CatalogRuleError.SmartConditionQueryRequired));
        }

        [Test]
        [FeatureTestCase(
            "Collection icon の範囲を検証する",
            "未定義の Collection icon 値を domain policy が拒否することを確認します。",
            order: 5)]
        public void EnsureCollectionIcon_RejectsUnsupportedValue()
        {
            var exception = Assert.Throws<CatalogRuleException>(
                () => CatalogCommandPolicy.EnsureCollectionIcon(6, 4));

            Assert.That(
                exception.Error,
                Is.EqualTo(CatalogRuleError.UnsupportedCollectionIcon));
        }
    }

    public sealed class CollectionPlacementPolicyTests
    {
        private static readonly CollectionPlacementNode[] Nodes =
        {
            new CollectionPlacementNode("a", null, false, 0),
            new CollectionPlacementNode("b", null, false, 1),
            new CollectionPlacementNode("a-child", "a", false, 0),
            new CollectionPlacementNode("smart", null, true, 2)
        };

        [Test]
        [FeatureTestCase(
            "Collection の親子同時選択を一度だけ移動する",
            "親と子を同時に選択した場合、top-level の親だけを placement 対象にすることを確認します。",
            order: 6)]
        public void Evaluate_RemovesSelectedDescendants()
        {
            var result = CollectionPlacementPolicy.Evaluate(
                Nodes,
                new[] { "a", "a-child" },
                null,
                1);

            Assert.That(result.IsValid, Is.True);
            Assert.That(
                result.MovingIds,
                Is.EqualTo(new[] { "a" }));
        }

        [Test]
        [FeatureTestCase(
            "Collection の循環配置を拒否する",
            "Collection を自身の子孫へ移動できないことを確認します。",
            order: 7)]
        public void Evaluate_RejectsDescendantParent()
        {
            var result = CollectionPlacementPolicy.Evaluate(
                Nodes,
                new[] { "a" },
                "a-child",
                0);

            Assert.That(
                result.Error,
                Is.EqualTo(CollectionPlacementError.Cycle));
        }

        [Test]
        [FeatureTestCase(
            "Smart Collection を親にできない",
            "通常 Collection の移動先に Smart Collection を指定した場合に拒否することを確認します。",
            order: 8)]
        public void Evaluate_RejectsSmartCollectionParent()
        {
            var result = CollectionPlacementPolicy.Evaluate(
                Nodes,
                new[] { "a" },
                "smart",
                0);

            Assert.That(
                result.Error,
                Is.EqualTo(
                    CollectionPlacementError
                        .SmartCollectionParent));
        }
    }
}
