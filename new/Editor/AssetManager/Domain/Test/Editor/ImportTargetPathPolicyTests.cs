using Ee4v.Core.Testing;
using NUnit.Framework;

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

    public sealed class AssetManagerDomainTestRegistrar : IFeatureTestRegistrar
    {
        public FeatureTestDescriptor CreateDescriptor()
        {
            return new FeatureTestDescriptor(
                "AssetManager Domain",
                "AssetManager",
                "Ee4v.AssetManager.Domain.Tests.Editor",
                "AssetManager の技術非依存な invariant と policy を確認します。",
                order: 301);
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
    }
}
