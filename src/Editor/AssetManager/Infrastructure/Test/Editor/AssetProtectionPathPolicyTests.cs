using System;
using System.Collections.Generic;
using Ee4v.AssetManager.Infrastructure.Unity;
using Ee4v.Testing.Contracts;
using NUnit.Framework;

namespace Ee4v.AssetManager.Infrastructure.Tests
{
    public sealed class AssetProtectionPathPolicyTests
    {
        [Test]
        [FeatureTestCase(
            "保護 folder の子孫と祖先 mutation を拒否する",
            "保護 root 配下の asset 編集に加え、root を含む親 folder の移動・削除も保護対象として判定することを確認します。",
            order: 337)]
        public void ProtectedRoot_ProtectsDescendantsAndAncestors()
        {
            var scopes = new[]
            {
                new AssetProtectionPathScope(
                    "guid",
                    "file",
                    "Assets/Avatar",
                    true)
            };

            Assert.That(
                AssetProtectionPathPolicy.IsProtected(
                    scopes,
                    new HashSet<string>(),
                    "Assets/Avatar/Materials/body.mat"),
                Is.True);
            Assert.That(
                AssetProtectionPathPolicy
                    .WouldMutateProtectedAsset(
                        scopes,
                        new HashSet<string>(),
                        "Assets"),
                Is.True);
            Assert.That(
                AssetProtectionPathPolicy.IsProtected(
                    scopes,
                    new HashSet<string>(),
                    "Assets/Editable/body.mat"),
                Is.False);
        }

        [Test]
        [FeatureTestCase(
            "より内側の保護解除と import 中の一時解除を優先する",
            "子 scope の解除が親 folder の保護より優先され、再 import 中は同じ file に属する scope だけが編集可能になることを確認します。",
            order: 338)]
        public void NestedAndSuspendedScopes_OverrideProtection()
        {
            var scopes = new[]
            {
                new AssetProtectionPathScope(
                    "root",
                    "file-1",
                    "Assets/Avatar",
                    true),
                new AssetProtectionPathScope(
                    "editable",
                    "file-2",
                    "Assets/Avatar/Editable",
                    false),
                new AssetProtectionPathScope(
                    "other",
                    "file-3",
                    "Assets/Other",
                    true)
            };

            Assert.That(
                AssetProtectionPathPolicy.IsProtected(
                    scopes,
                    new HashSet<string>(),
                    "Assets/Avatar/Editable/body.mat"),
                Is.False);
            Assert.That(
                AssetProtectionPathPolicy.IsProtected(
                    scopes,
                    new HashSet<string>(
                        new[] { "file-1" },
                        StringComparer.Ordinal),
                    "Assets/Avatar/body.mat"),
                Is.False);
            Assert.That(
                AssetProtectionPathPolicy.IsProtected(
                    scopes,
                    new HashSet<string>(
                        new[] { "file-1" },
                        StringComparer.Ordinal),
                    "Assets/Other/body.mat"),
                Is.True);
        }
    }
}
