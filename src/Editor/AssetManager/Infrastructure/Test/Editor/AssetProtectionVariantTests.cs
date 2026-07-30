using Ee4v.AssetManager.Infrastructure.Unity;
using Ee4v.Testing.Contracts;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Ee4v.AssetManager.Infrastructure.Tests
{
    public sealed class AssetProtectionVariantTests
    {
        private const string Root =
            "Assets/__ee4v_asset_protection_variant_tests";

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(Root);
            AssetDatabase.CreateFolder(
                "Assets",
                "__ee4v_asset_protection_variant_tests");
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(Root);
        }

        [Test]
        [FeatureTestCase(
            "保護 Material から Material Variant を作成する",
            "派生 Material の parent が元 Material を参照し、原本を変更しないことを確認します。",
            order: 317)]
        public void CreateMaterialVariant_SetsProtectedSourceAsParent()
        {
            var sourcePath = Root + "/Source.mat";
            var destinationPath =
                Root + "/Source Variant.mat";
            var source =
                new Material(
                    Shader.Find("Standard"));
            AssetDatabase.CreateAsset(source, sourcePath);
            var guid =
                AssetDatabase.AssetPathToGUID(sourcePath);
            var service =
                new AssetProtectionService();

            var created =
                service.CreateMaterialVariant(
                    guid,
                    destinationPath);
            var variant =
                AssetDatabase.LoadAssetAtPath<Material>(
                    destinationPath);

            Assert.That(created, Is.True);
            Assert.That(variant, Is.Not.Null);
            Assert.That(variant.isVariant, Is.True);
            Assert.That(
                variant.parent.GetInstanceID(),
                Is.EqualTo(source.GetInstanceID()));
        }

        [Test]
        [FeatureTestCase(
            "保護 Prefab から Prefab Variant を作成する",
            "派生 Prefab が元 Prefab をsourceに持つVariantとして保存されることを確認します。",
            order: 318)]
        public void CreatePrefabVariant_SetsProtectedSourceAsParent()
        {
            var sourcePath = Root + "/Source.prefab";
            var destinationPath =
                Root + "/Source Variant.prefab";
            var sourceObject =
                new GameObject("Source");
            var source =
                PrefabUtility.SaveAsPrefabAsset(
                    sourceObject,
                    sourcePath);
            Object.DestroyImmediate(sourceObject);
            var guid =
                AssetDatabase.AssetPathToGUID(sourcePath);
            var service =
                new AssetProtectionService();

            var created =
                service.CreatePrefabVariant(
                    guid,
                    destinationPath);
            var variant =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    destinationPath);

            Assert.That(created, Is.True);
            Assert.That(variant, Is.Not.Null);
            Assert.That(
                PrefabUtility.GetPrefabAssetType(variant),
                Is.EqualTo(PrefabAssetType.Variant));
            Assert.That(
                PrefabUtility
                    .GetCorrespondingObjectFromSource(
                        variant)
                    .GetInstanceID(),
                Is.EqualTo(source.GetInstanceID()));
        }
    }
}
