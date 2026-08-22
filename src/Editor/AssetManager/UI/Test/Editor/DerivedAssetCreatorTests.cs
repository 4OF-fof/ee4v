using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Ee4v.AssetManager.UI.Tests
{
    internal sealed class DerivedAssetCreatorTests
    {
        private const string SourceFolder =
            "Assets/__Ee4vDerivedAssetCreatorTests";
        private const string OutputName =
            "__Ee4vDerivedAssetCreatorTestOutput";
        private const string ParentItemId = "derived-asset-test-item";

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(
                DerivedAssetCreator.GetVariantFolder(OutputName));
            AssetDatabase.DeleteAsset(SourceFolder);
            AssetDatabase.CreateFolder(
                "Assets",
                "__Ee4vDerivedAssetCreatorTests");
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(
                DerivedAssetCreator.GetVariantFolder(OutputName));
            AssetDatabase.DeleteAsset(SourceFolder);
        }

        [Test]
        public void Create_ProducesVariantAndReplacesEditableDependencies()
        {
            var sourceMaterial = new Material(
                Shader.Find("Standard"));
            var materialPath = SourceFolder + "/Source.mat";
            AssetDatabase.CreateAsset(sourceMaterial, materialPath);

            var sourceClip = new AnimationClip();
            var clipPath = SourceFolder + "/Source.anim";
            AssetDatabase.CreateAsset(sourceClip, clipPath);
            var controllerPath = SourceFolder + "/Source.controller";
            var sourceController = AnimatorController
                .CreateAnimatorControllerAtPath(controllerPath);
            sourceController.AddMotion(sourceClip);

            var sourceObject = new GameObject("Source");
            sourceObject.AddComponent<MeshRenderer>().sharedMaterial =
                sourceMaterial;
            sourceObject.AddComponent<Animator>().runtimeAnimatorController =
                sourceController;
            var prefabPath = SourceFolder + "/Source.prefab";
            var sourcePrefab = PrefabUtility.SaveAsPrefabAsset(
                sourceObject,
                prefabPath);
            Object.DestroyImmediate(sourceObject);
            AssetDatabase.SaveAssets();

            var result = DerivedAssetCreator.Create(
                new DerivedAssetCreationRequest
                {
                    ParentItemId = ParentItemId,
                    Name = OutputName,
                    Description = "Derived description",
                    Prefab = sourcePrefab
                });

            Assert.That(
                PrefabUtility.GetPrefabAssetType(result.Prefab),
                Is.EqualTo(PrefabAssetType.Variant));
            Assert.That(
                result.AssetPath,
                Is.EqualTo(
                    DerivedAssetCreator.GetVariantFolder(OutputName) +
                    "/" + OutputName + ".prefab"));

            var renderer = result.Prefab.GetComponent<MeshRenderer>();
            var derivedMaterial = renderer.sharedMaterial;
            Assert.That(derivedMaterial.isVariant, Is.True);
            Assert.That(
                AssetDatabase.GetAssetPath(derivedMaterial.parent),
                Is.EqualTo(materialPath));
            Assert.That(
                AssetDatabase.GetAssetPath(derivedMaterial),
                Does.StartWith(
                    DerivedAssetCreator.GetVariantFolder(OutputName) +
                    "/Assets/"));

            var animator = result.Prefab.GetComponent<Animator>();
            var derivedController =
                animator.runtimeAnimatorController as AnimatorController;
            Assert.That(derivedController, Is.Not.Null);
            Assert.That(
                AssetDatabase.GetAssetPath(derivedController),
                Is.Not.EqualTo(controllerPath));
            var derivedClip = derivedController.layers[0]
                .stateMachine.states
                .Select(state => state.state.motion)
                .OfType<AnimationClip>()
                .Single();
            Assert.That(
                AssetDatabase.GetAssetPath(derivedClip),
                Is.Not.EqualTo(clipPath));
            Assert.That(
                AssetDatabase.GetAssetPath(derivedClip),
                Does.StartWith(
                    DerivedAssetCreator.GetVariantFolder(OutputName) +
                    "/Assets/"));

            var listed = DerivedAssetCreator.FindByParentItem(ParentItemId);
            Assert.That(listed.Count, Is.EqualTo(1));
            Assert.That(
                listed[0].Description,
                Is.EqualTo("Derived description"));
            Assert.That(
                AssetDatabase.GetAssetPath(listed[0].Prefab),
                Is.EqualTo(result.AssetPath));
        }

        [Test]
        public void FindPrefabCandidates_ResolvesOnlyImportedPrefabs()
        {
            var materialPath = SourceFolder + "/Candidate.mat";
            AssetDatabase.CreateAsset(
                new Material(Shader.Find("Standard")),
                materialPath);
            var sourceObject = new GameObject("Candidate");
            var prefabPath = SourceFolder + "/Candidate.prefab";
            PrefabUtility.SaveAsPrefabAsset(sourceObject, prefabPath);
            Object.DestroyImmediate(sourceObject);

            var candidates = DerivedAssetCreator.FindPrefabCandidates(
                new[]
                {
                    AssetDatabase.AssetPathToGUID(materialPath),
                    AssetDatabase.AssetPathToGUID(prefabPath),
                    AssetDatabase.AssetPathToGUID(prefabPath),
                    "00000000000000000000000000000000"
                });

            Assert.That(candidates.Count, Is.EqualTo(1));
            Assert.That(
                AssetDatabase.GetAssetPath(candidates[0]),
                Is.EqualTo(prefabPath));
        }
    }
}
