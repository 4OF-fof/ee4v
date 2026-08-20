using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Ee4v.FaceExpression.Tests
{
    public sealed class FaceExpressionTests
    {
        private const string TestFolder = "Assets/FaceExpressionTests";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(TestFolder))
            {
                AssetDatabase.CreateFolder("Assets", "FaceExpressionTests");
            }
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(TestFolder);
        }

        [Test]
        public void Apply_PreservesExistingLayersAndNormalizesExpressionClips()
        {
            var avatar = CreateAvatar(out var mesh);
            try
            {
                var smile = CreateClip(
                    "Smile.anim",
                    ("blendShape.Smile", 80f));
                var angry = CreateClip(
                    "Angry.anim",
                    ("blendShape.Angry", 65f));
                var controller = AnimatorController.CreateAnimatorControllerAtPath(
                    TestFolder + "/Avatar.controller");
                controller.AddLayer("Existing Layer");

                FaceExpressionControllerWriter.Apply(
                    controller,
                    avatar,
                    new Dictionary<FaceGesture, AnimationClip>
                    {
                        [FaceGesture.Fist] = smile,
                        [FaceGesture.Victory] = angry
                    },
                    TestFolder);

                Assert.That(
                    controller.layers.Select(layer => layer.name),
                    Does.Contain("Existing Layer"));
                Assert.That(
                    controller.layers.Count(layer =>
                        layer.name == FaceExpressionControllerWriter.LayerName),
                    Is.EqualTo(1));
                var assignments = FaceExpressionControllerWriter.Read(controller);
                Assert.That(assignments[FaceGesture.Fist], Is.SameAs(smile));
                Assert.That(assignments[FaceGesture.Victory], Is.SameAs(angry));

                var layer = controller.layers.Single(candidate =>
                    candidate.name == FaceExpressionControllerWriter.LayerName);
                var fist = layer.stateMachine.states
                    .Select(child => child.state)
                    .Single(state => state.name == "01 Fist");
                var generated = (AnimationClip)fist.motion;
                Assert.That(ReadValue(generated, "blendShape.Smile"), Is.EqualTo(80f));
                Assert.That(ReadValue(generated, "blendShape.Angry"), Is.EqualTo(5f));

                FaceExpressionControllerWriter.Apply(
                    controller,
                    avatar,
                    new Dictionary<FaceGesture, AnimationClip>
                    {
                        [FaceGesture.Fist] = smile
                    },
                    TestFolder);
                Assert.That(
                    controller.layers.Count(candidate =>
                        candidate.name == FaceExpressionControllerWriter.LayerName),
                    Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(avatar);
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void Read_ConvertsConfiguredSeparatorShapesToHeaders()
        {
            var avatar = CreateAvatar(
                out var mesh,
                "----------EYE------------",
                "Blink",
                "====MOUTH====",
                "Smile");
            try
            {
                var channels = FaceExpressionClipEditor.Read(
                    avatar,
                    null,
                    new[] { "=", "-" });

                Assert.That(channels[0].IsHeader, Is.True);
                Assert.That(channels[0].HeaderText, Is.EqualTo("EYE"));
                Assert.That(channels[1].IsHeader, Is.False);
                Assert.That(channels[2].IsHeader, Is.True);
                Assert.That(channels[2].HeaderText, Is.EqualTo("MOUTH"));
            }
            finally
            {
                Object.DestroyImmediate(avatar);
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void Groups_CountAndFilterShapesBetweenHeaders()
        {
            var channels = new[]
            {
                new BlendShapeChannel("Body", "Smile", 0f, false),
                new BlendShapeChannel("Body", "---EYES---", 0f, false, "EYES"),
                new BlendShapeChannel("Body", "Blink", 0f, false),
                new BlendShapeChannel("Body", "Wide", 0f, false),
                new BlendShapeChannel("Body", "---MOUTH---", 0f, false, "MOUTH"),
                new BlendShapeChannel("Body", "Aa", 0f, false)
            };

            FaceExpressionGroupSession.UpdateChannels(channels);

            Assert.That(FaceExpressionGroupSession.TotalCount, Is.EqualTo(4));
            Assert.That(
                FaceExpressionGroupSession.Groups.Select(group =>
                    (group.Name, group.Count)),
                Is.EqualTo(new[] { ("EYES", 2), ("MOUTH", 1) }));

            FaceExpressionGroupSession.SelectGroup("MOUTH");
            Assert.That(
                FaceExpressionGroupSession.Filter(channels)
                    .Select(channel => channel.Name),
                Is.EqualTo(new[] { "---MOUTH---", "Aa" }));
            FaceExpressionGroupSession.SelectGroup(null);
        }

        private static GameObject CreateAvatar(
            out Mesh mesh,
            params string[] shapeNames)
        {
            var avatar = new GameObject("Avatar");
            var body = new GameObject("Body");
            body.transform.SetParent(avatar.transform, false);
            var renderer = body.AddComponent<SkinnedMeshRenderer>();
            mesh = new Mesh
            {
                vertices = new[]
                {
                    Vector3.zero,
                    Vector3.right,
                    Vector3.up
                },
                triangles = new[] { 0, 1, 2 }
            };
            var delta = new[] { Vector3.zero, Vector3.zero, Vector3.zero };
            if (shapeNames.Length == 0)
            {
                shapeNames = new[] { "Smile", "Angry" };
            }

            foreach (var shapeName in shapeNames)
            {
                mesh.AddBlendShapeFrame(shapeName, 100f, delta, delta, delta);
            }

            renderer.sharedMesh = mesh;
            if (shapeNames.Length > 1 && shapeNames[1] == "Angry")
            {
                renderer.SetBlendShapeWeight(1, 5f);
            }

            return avatar;
        }

        private static AnimationClip CreateClip(
            string fileName,
            params (string property, float value)[] values)
        {
            var clip = new AnimationClip();
            foreach (var value in values)
            {
                AnimationUtility.SetEditorCurve(
                    clip,
                    EditorCurveBinding.FloatCurve(
                        "Body",
                        typeof(SkinnedMeshRenderer),
                        value.property),
                    AnimationCurve.Constant(0f, 1f / 60f, value.value));
            }

            AssetDatabase.CreateAsset(clip, TestFolder + "/" + fileName);
            return clip;
        }

        private static float ReadValue(AnimationClip clip, string property)
        {
            var curve = AnimationUtility.GetEditorCurve(
                clip,
                EditorCurveBinding.FloatCurve(
                    "Body",
                    typeof(SkinnedMeshRenderer),
                    property));
            Assert.That(curve, Is.Not.Null);
            return curve.Evaluate(0f);
        }
    }
}
