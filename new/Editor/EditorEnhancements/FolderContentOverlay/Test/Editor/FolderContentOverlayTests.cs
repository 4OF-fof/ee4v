using System.Collections.Generic;
using System.IO;
using Ee4v.Core.Injector;
using Ee4v.Testing.Contracts;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Ee4v.FolderContentOverlay.Tests
{
    public sealed class FolderContentOverlayTests
    {
        private const string TestRoot =
            "Assets/__Ee4vFolderContentOverlayTests";

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(TestRoot);
            AssetDatabase.CreateFolder(
                "Assets",
                "__Ee4vFolderContentOverlayTests");
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(TestRoot);
        }

        [Test]
        [FeatureTestCase(
            "FolderContentOverlay のgrid配置を計算できる",
            "Project Window のgrid表示でオーバーレイがフォルダアイコン右下へ配置されることを確認します。",
            order: 10)]
        public void Layout_PlacesOverlayAtBottomRight()
        {
            var itemRect = new Rect(10f, 20f, 64f, 80f);

            var iconRect =
                FolderContentOverlayLayout.GetFolderIconRect(
                    itemRect,
                    ProjectItemViewMode.TwoColumns,
                    ProjectItemOrientation.Vertical);
            var overlayRect =
                FolderContentOverlayLayout.GetOverlayRect(iconRect);

            Assert.That(iconRect.x, Is.EqualTo(9f));
            Assert.That(iconRect.y, Is.EqualTo(19f));
            Assert.That(iconRect.width, Is.EqualTo(66f));
            Assert.That(iconRect.height, Is.EqualTo(62.7f).Within(0.001f));
            Assert.That(overlayRect.xMax, Is.EqualTo(iconRect.xMax));
            Assert.That(overlayRect.yMax, Is.EqualTo(iconRect.yMax));
            Assert.That(
                overlayRect.size,
                Is.EqualTo(iconRect.size * 0.5f));
        }

        [Test]
        [FeatureTestCase(
            "FolderContentOverlay の設定は既定で有効",
            "FolderContentOverlay が初回起動時に有効であることを確認します。",
            order: 20)]
        public void Definition_IsEnabledByDefault()
        {
            Assert.That(
                FolderContentOverlayDefinitions.Enabled.DefaultValue,
                Is.EqualTo(true));
        }

        [Test]
        [FeatureTestCase(
            "同じ代表iconを親folderへ伝播できる",
            "子folderがすべて同じ代表iconを持つ場合に祖先folderも同じiconを返すことを確認します。",
            order: 30)]
        public void IconCache_PropagatesSharedRepresentativeToAncestors()
        {
            var parent = CreateFolder(TestRoot, "Parent");
            var first = CreateFolder(parent, "First");
            var second = CreateFolder(parent, "Second");
            AssetDatabase.CreateAsset(
                new AnimationClip(),
                first + "/First.anim");
            AssetDatabase.CreateAsset(
                new AnimationClip(),
                second + "/Second.anim");
            AssetDatabase.SaveAssets();

            var cache = new FolderContentIconCache(
                new FolderAssetIconResolver());
            var firstIcon = cache.Get(first);
            var secondIcon = cache.Get(second);
            var parentIcon = cache.Get(parent);
            var rootIcon = cache.Get(TestRoot);

            Assert.That(firstIcon, Is.Not.Null);
            Assert.That(secondIcon, Is.EqualTo(firstIcon));
            Assert.That(parentIcon, Is.EqualTo(firstIcon));
            Assert.That(rootIcon, Is.EqualTo(firstIcon));
        }

        [Test]
        [FeatureTestCase(
            "Prefabとmodelへ別のiconを割り当てる",
            "main asset typeがGameObjectでもpath固有iconによってPrefabとmodelを区別できることを確認します。",
            order: 40)]
        public void AssetIconResolver_DistinguishesPrefabAndModel()
        {
            var prefabPath = TestRoot + "/Sample.prefab";
            var modelPath = TestRoot + "/Sample.obj";
            var gameObject = new GameObject("Sample");
            try
            {
                PrefabUtility.SaveAsPrefabAsset(gameObject, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }

            File.WriteAllText(
                Path.GetFullPath(modelPath),
                "o Sample\n" +
                "v 0 0 0\n" +
                "v 1 0 0\n" +
                "v 0 1 0\n" +
                "f 1 2 3\n");
            AssetDatabase.ImportAsset(
                modelPath,
                ImportAssetOptions.ForceSynchronousImport);

            var resolver = new FolderAssetIconResolver();
            var prefabIcon = resolver.Resolve(prefabPath);
            var modelIcon = resolver.Resolve(modelPath);

            Assert.That(prefabIcon, Is.Not.Null);
            Assert.That(modelIcon, Is.Not.Null);
            Assert.That(
                modelIcon,
                Is.Not.EqualTo(prefabIcon),
                "Prefab icon: " + prefabIcon.name +
                ", model icon: " + modelIcon.name);
        }

        [Test]
        [FeatureTestCase(
            "asset変更時に祖先folderを列挙できる",
            "子assetの変更によって再計算が必要になる直接の親からAssets rootまでを列挙できることを確認します。",
            order: 50)]
        public void AssetPostprocessor_CollectsAncestorFolders()
        {
            var affectedFolders = new HashSet<string>();

            FolderContentOverlayAssetPostprocessor
                .CollectFolderAndAncestors(
                    "Assets/Avatar/Animation",
                    affectedFolders);

            Assert.That(
                affectedFolders,
                Is.EquivalentTo(new[]
                {
                    "Assets/Avatar/Animation",
                    "Assets/Avatar",
                    "Assets"
                }));
        }

        [Test]
        [FeatureTestCase(
            "親への伝播には過半数を要求する",
            "最多iconが半数ちょうどの場合は伝播せず、50%を超えた場合だけ伝播することを確認します。",
            order: 60)]
        public void RepresentativeIcon_RequiresStrictMajorityToPropagate()
        {
            var primary = new Texture2D(1, 1)
            {
                name = "Primary"
            };
            var secondary = new Texture2D(1, 1)
            {
                name = "Secondary"
            };

            try
            {
                Assert.That(
                    FolderContentIconCache
                        .SelectMajorityRepresentativeIcon(new Texture[]
                        {
                            primary,
                            secondary
                        }),
                    Is.Null);
                Assert.That(
                    FolderContentIconCache
                        .SelectMajorityRepresentativeIcon(new Texture[]
                        {
                            primary,
                            primary,
                            secondary
                        }),
                    Is.EqualTo(primary));
            }
            finally
            {
                Object.DestroyImmediate(primary);
                Object.DestroyImmediate(secondary);
            }
        }

        [Test]
        [FeatureTestCase(
            "最多iconが同数なら代表を表示しない",
            "複数のiconが同じ最多数を持つ場合は名前順で選ばず、代表iconなしになることを確認します。",
            order: 65)]
        public void RepresentativeIcon_ReturnsNullWhenMostCommonIsTied()
        {
            var primary = new Texture2D(1, 1)
            {
                name = "Primary"
            };
            var secondary = new Texture2D(1, 1)
            {
                name = "Secondary"
            };

            try
            {
                Assert.That(
                    FolderContentIconCache.SelectRepresentativeIcon(
                        new Texture[]
                        {
                            primary,
                            secondary
                        }),
                    Is.Null);
                Assert.That(
                    FolderContentIconCache.SelectRepresentativeIcon(
                        new Texture[]
                        {
                            primary,
                            primary,
                            secondary
                        }),
                    Is.EqualTo(primary));
            }
            finally
            {
                Object.DestroyImmediate(primary);
                Object.DestroyImmediate(secondary);
            }
        }

        [Test]
        [FeatureTestCase(
            "実体previewを固定の種別iconへ置換する",
            "Texture、Material、Meshの内容に依存するpreviewが共通の種別iconへ正規化されることを確認します。",
            order: 70)]
        public void AssetIconResolver_UsesStableIconsForContentPreviews()
        {
            var texturePath = TestRoot + "/Sample.png";
            var materialPath = TestRoot + "/Sample.mat";
            var meshPath = TestRoot + "/Sample.asset";
            var texture = new Texture2D(1, 1);
            try
            {
                texture.SetPixel(0, 0, Color.magenta);
                texture.Apply();
                File.WriteAllBytes(
                    Path.GetFullPath(texturePath),
                    texture.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }

            AssetDatabase.ImportAsset(
                texturePath,
                ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.CreateAsset(
                new Material(Shader.Find("Standard")),
                materialPath);
            AssetDatabase.CreateAsset(
                new Mesh(),
                meshPath);
            AssetDatabase.SaveAssets();

            var resolver = new FolderAssetIconResolver();

            AssertStableTypeIcon(
                resolver,
                texturePath,
                "Texture Icon");
            AssertStableTypeIcon(
                resolver,
                materialPath,
                "Material Icon");
            AssertStableTypeIcon(
                resolver,
                meshPath,
                "Mesh Icon");
        }

        private static string CreateFolder(
            string parentFolder,
            string folderName)
        {
            var guid = AssetDatabase.CreateFolder(
                parentFolder,
                folderName);
            return AssetDatabase.GUIDToAssetPath(guid);
        }

        private static void AssertStableTypeIcon(
            FolderAssetIconResolver resolver,
            string assetPath,
            string expectedIconName)
        {
            var resolvedIcon = resolver.Resolve(assetPath);
            var expectedIcon =
                EditorGUIUtility.IconContent(expectedIconName).image;

            Assert.That(resolvedIcon, Is.Not.Null);
            Assert.That(resolvedIcon, Is.EqualTo(expectedIcon));
        }
    }
}
