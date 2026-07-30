using System;
using System.Collections.Generic;
using Ee4v.Testing.Contracts;
using NUnit.Framework;

namespace Ee4v.AssetManager.Domain.Tests
{
    public sealed class FileDependencyGraphPolicyTests
    {
        [Test]
        [FeatureTestCase(
            "File dependency を import 順へ解決する",
            "共有 dependency を一度だけ、すべての dependent file より前に並べることを確認します。",
            order: 7)]
        public void ResolveImportOrder_RecursiveDependencies_ReturnsDependenciesFirst()
        {
            var graph = new Dictionary<string, IReadOnlyList<string>>
            {
                { "file-a", new[] { "file-b", "file-c" } },
                { "file-b", new[] { "file-c" } },
                { "file-c", Array.Empty<string>() }
            };

            var order =
                FileDependencyGraphPolicy.ResolveImportOrder(
                    "file-a",
                    fileId => graph[fileId]);

            Assert.That(
                order,
                Is.EqualTo(
                    new[]
                    {
                        "file-c",
                        "file-b",
                        "file-a"
                    }));
        }

        [Test]
        [FeatureTestCase(
            "間接的な循環 dependency を拒否する",
            "置換予定の edge から既存 graph を辿って source へ戻る場合は登録前に拒否することを確認します。",
            order: 8)]
        public void EnsureCanReplace_IndirectCycle_Throws()
        {
            var graph = new Dictionary<string, IReadOnlyList<string>>
            {
                { "file-b", new[] { "file-c" } },
                { "file-c", new[] { "file-a" } }
            };

            var exception =
                Assert.Throws<CatalogRuleException>(
                    () =>
                        FileDependencyGraphPolicy
                            .EnsureCanReplace(
                                "file-a",
                                new[] { "file-b" },
                                fileId =>
                                    graph.TryGetValue(
                                        fileId,
                                        out var dependencies)
                                        ? dependencies
                                        : Array.Empty<string>()));

            Assert.That(
                exception.Error,
                Is.EqualTo(
                    CatalogRuleError.DependencyCycle));
        }
    }
}
