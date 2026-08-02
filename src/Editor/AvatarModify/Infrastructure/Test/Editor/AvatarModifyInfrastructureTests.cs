using Ee4v.AssetManager.Contracts;
using Ee4v.AvatarModify.Application;
using Ee4v.AvatarModify.Infrastructure.Unity;
using Ee4v.Testing.Contracts;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[assembly: FeatureTestSuite(
    "AvatarModify Infrastructure",
    "AvatarModify",
    "Ee4v.AvatarModify.Infrastructure.Tests.Editor",
    "PrefabとMaterialのVariant作成境界を確認します。",
    order: 307)]

namespace Ee4v.AvatarModify.Infrastructure.Tests
{
    public sealed class AvatarVariantGatewayTests
    {
        private const string Root =
            "Assets/__Ee4vAvatarModifyTests";
        private string _basePrefabGuid;
        private string _materialGuid;
        private string _nestedPrefabGuid;
        private string _prefabGuid;
        private string _textureGuid;

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(Root);
            AssetDatabase.CreateFolder(
                "Assets",
                "__Ee4vAvatarModifyTests");
            var texture = new Texture2D(2, 2)
            {
                name = "Source Texture"
            };
            AssetDatabase.CreateAsset(
                texture,
                Root + "/Source.asset");
            _textureGuid = AssetDatabase.AssetPathToGUID(
                Root + "/Source.asset");

            var shader = Shader.Find("Standard") ??
                         Shader.Find(
                             "Hidden/InternalErrorShader");
            var material = new Material(shader)
            {
                name = "Source Material",
                mainTexture = texture
            };
            AssetDatabase.CreateAsset(
                material,
                Root + "/Source.mat");
            _materialGuid = AssetDatabase.AssetPathToGUID(
                Root + "/Source.mat");

            var accessory = new GameObject("Accessory");
            accessory.AddComponent<MeshRenderer>()
                .sharedMaterial = material;
            PrefabUtility.SaveAsPrefabAsset(
                accessory,
                Root + "/Accessory.prefab");
            Object.DestroyImmediate(accessory);
            _nestedPrefabGuid = AssetDatabase.AssetPathToGUID(
                Root + "/Accessory.prefab");

            var avatar = new GameObject("Source Avatar");
            var nested = PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    Root + "/Accessory.prefab")) as GameObject;
            nested.transform.SetParent(avatar.transform, false);
            PrefabUtility.SaveAsPrefabAsset(
                avatar,
                Root + "/Base.prefab");
            Object.DestroyImmediate(avatar);
            _basePrefabGuid = AssetDatabase.AssetPathToGUID(
                Root + "/Base.prefab");

            var sourceVariant = PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    Root + "/Base.prefab")) as GameObject;
            PrefabUtility.SaveAsPrefabAsset(
                sourceVariant,
                Root + "/Source.prefab");
            Object.DestroyImmediate(sourceVariant);
            _prefabGuid = AssetDatabase.AssetPathToGUID(
                Root + "/Source.prefab");
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(Root);
        }

        [Test]
        [FeatureTestCase(
            "対象PrefabだけにVariantを1段追加する",
            "ネストPrefabは元参照を維持し、MaterialだけをVariantへ置換します。",
            order: 1)]
        public void Create_CreatesMaterialVariantsAndKeepsTextureReference()
        {
            var derivation = new TestDerivationService(
                _materialGuid,
                false,
                _prefabGuid,
                _basePrefabGuid,
                _nestedPrefabGuid);
            var gateway = new UnityAvatarVariantGateway(
                derivation);

            var result = gateway.Create(
                new VariantAssetRequest
                {
                    VariantName = "Avatar",
                    SourcePrefabGuid = _prefabGuid,
                    DestinationRoot = Root + "/Variants"
                });

            Assert.That(result.Succeeded, Is.True, result.Error);
            Assert.That(
                derivation.PrefabVariantAttempts,
                Is.EqualTo(1));
            Assert.That(
                derivation.MaterialVariantAttempts,
                Is.EqualTo(1));
            var variant =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    AssetDatabase.GUIDToAssetPath(
                        result.VariantPrefabGuid));
            var variantParent =
                PrefabUtility.GetCorrespondingObjectFromSource(
                    variant);
            Assert.That(
                AssetDatabase.AssetPathToGUID(
                    AssetDatabase.GetAssetPath(variantParent)),
                Is.EqualTo(_prefabGuid));
            var material = variant
                .GetComponentInChildren<MeshRenderer>()
                .sharedMaterial;
            Assert.That(
                AssetDatabase.AssetPathToGUID(
                    AssetDatabase.GetAssetPath(material)),
                Is.Not.EqualTo(_materialGuid));
            Assert.That(
                AssetDatabase.AssetPathToGUID(
                    AssetDatabase.GetAssetPath(
                        material.mainTexture)),
                Is.EqualTo(_textureGuid));
            var nested = variant.transform.GetChild(0).gameObject;
            var nestedSource =
                PrefabUtility.GetCorrespondingObjectFromSource(
                    nested);
            Assert.That(
                AssetDatabase.AssetPathToGUID(
                    AssetDatabase.GetAssetPath(nestedSource)),
                Is.EqualTo(_nestedPrefabGuid));
        }

        [Test]
        [FeatureTestCase(
            "Variant作成失敗時に今回のassetを戻す",
            "Material Variant作成に失敗した場合、途中作成したPrefabとfolderを残しません。",
            order: 2)]
        public void Create_RollsBackFailedOperation()
        {
            var gateway = new UnityAvatarVariantGateway(
                new TestDerivationService(
                    _materialGuid,
                    true,
                    _prefabGuid,
                    _basePrefabGuid,
                    _nestedPrefabGuid));

            var result = gateway.Create(
                new VariantAssetRequest
                {
                    VariantName = "Failed",
                    SourcePrefabGuid = _prefabGuid,
                    DestinationRoot = Root + "/Variants"
                });

            Assert.That(result.Succeeded, Is.False);
            Assert.That(
                AssetDatabase.IsValidFolder(
                    Root + "/Variants/Failed"),
                Is.False);
            Assert.That(
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    Root + "/Source.prefab"),
                Is.Not.Null);
        }

        private sealed class TestDerivationService :
            IAssetManagerAssetDerivationService
        {
            private readonly string _materialGuid;
            private readonly HashSet<string> _prefabGuids;
            private readonly bool _failMaterial;

            internal TestDerivationService(
                string materialGuid,
                bool failMaterial,
                params string[] prefabGuids)
            {
                _materialGuid = materialGuid;
                _prefabGuids = new HashSet<string>(
                    prefabGuids);
                _failMaterial = failMaterial;
            }

            internal int PrefabVariantAttempts { get; private set; }
            internal int MaterialVariantAttempts { get; private set; }

            public bool IsManaged(string assetGuid) => true;
            public bool IsProtected(string assetGuid) => false;
            public bool CanCreateMaterialVariant(
                string assetGuid) => assetGuid == _materialGuid;
            public bool CanCreatePrefabVariant(
                string assetGuid) =>
                _prefabGuids.Contains(assetGuid);

            public bool CreateEditableCopy(
                string assetGuid,
                string destinationAssetPath) => false;

            public bool CreateMaterialVariant(
                string assetGuid,
                string destinationAssetPath)
            {
                MaterialVariantAttempts++;
                if (_failMaterial)
                {
                    return false;
                }

                var parent =
                    AssetDatabase.LoadAssetAtPath<Material>(
                        AssetDatabase.GUIDToAssetPath(
                            assetGuid));
                var variant = new Material(parent)
                {
                    parent = parent
                };
                AssetDatabase.CreateAsset(
                    variant,
                    destinationAssetPath);
                return true;
            }

            public bool CreatePrefabVariant(
                string assetGuid,
                string destinationAssetPath)
            {
                PrefabVariantAttempts++;
                var source =
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        AssetDatabase.GUIDToAssetPath(
                            assetGuid));
                var instance = PrefabUtility.InstantiatePrefab(
                    source) as GameObject;
                try
                {
                    return PrefabUtility.SaveAsPrefabAsset(
                               instance,
                               destinationAssetPath) != null;
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }
            }
        }
    }
}
