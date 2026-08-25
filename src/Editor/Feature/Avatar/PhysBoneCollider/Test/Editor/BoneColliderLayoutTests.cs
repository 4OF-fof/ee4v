using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Ee4v.PhysBoneCollider.Tests
{
    public sealed class BoneColliderLayoutTests
    {
        [Test]
        public void Create_UsesOnlyVisibleMajorBonesInDirectArmature()
        {
            var avatar = new GameObject("Avatar");
            try
            {
                var armature = new GameObject("Armature").transform;
                armature.SetParent(avatar.transform, false);
                var longBone = new GameObject("Hips").transform;
                longBone.SetParent(armature, false);
                var nextBone = new GameObject("Spine").transform;
                nextBone.SetParent(longBone, false);
                nextBone.localPosition = Vector3.up * 0.2f;
                var shortLeaf = new GameObject("Chest").transform;
                shortLeaf.SetParent(nextBone, false);
                shortLeaf.localPosition = Vector3.up * 0.02f;
                var neck = new GameObject("Neck").transform;
                neck.SetParent(shortLeaf, false);
                neck.localPosition = Vector3.up * 0.12f;

                var leftLeg = new GameObject("LeftUpperLeg").transform;
                leftLeg.SetParent(longBone, false);
                leftLeg.localPosition = Vector3.left * 0.1f;
                var rightLeg = new GameObject("RightUpperLeg").transform;
                rightLeg.SetParent(longBone, false);
                rightLeg.localPosition = Vector3.right * 0.1f;
                var leftArm = new GameObject("LeftUpperArm").transform;
                leftArm.SetParent(shortLeaf, false);
                leftArm.localPosition = Vector3.left * 0.2f;
                var rightArm = new GameObject("RightUpperArm").transform;
                rightArm.SetParent(shortLeaf, false);
                rightArm.localPosition = Vector3.right * 0.2f;

                var head = new GameObject("Head").transform;
                head.SetParent(armature, false);
                var eye = new GameObject("LeftEye").transform;
                eye.SetParent(head, false);
                eye.localPosition = Vector3.up * 0.2f;

                var editorOnly = new GameObject("EditorOnlyBones").transform;
                editorOnly.SetParent(armature, false);
                editorOnly.tag = "EditorOnly";
                var editorArm = new GameObject("RightUpperArm").transform;
                editorArm.SetParent(editorOnly, false);
                var editorForearm = new GameObject("RightLowerArm").transform;
                editorForearm.SetParent(editorArm, false);
                editorForearm.localPosition = Vector3.right * 0.3f;

                var renderer = avatar.AddComponent<SkinnedMeshRenderer>();
                var outside = new GameObject("LeftUpperArm").transform;
                outside.SetParent(avatar.transform, false);
                var outsideNext = new GameObject("LeftLowerArm").transform;
                outsideNext.SetParent(outside, false);
                outsideNext.localPosition = Vector3.up * 0.3f;
                renderer.bones = new[]
                {
                    longBone,
                    nextBone,
                    shortLeaf,
                    neck,
                    leftLeg,
                    rightLeg,
                    leftArm,
                    rightArm,
                    head,
                    eye,
                    editorArm,
                    editorForearm,
                    outside,
                    outsideNext
                };

                var result = BoneColliderLayout.Create(avatar, 0.08f);

                Assert.That(result.Count, Is.EqualTo(2));
                var lowerTorso = result.Single(item => item.Bone == longBone);
                Assert.That(lowerTorso.Position.y, Is.EqualTo(0.11f).Within(0.0001f));
                Assert.That(lowerTorso.Radius, Is.EqualTo(0.11f).Within(0.0001f));
                Assert.That(lowerTorso.Height, Is.EqualTo(0.22f).Within(0.0001f));
                Assert.That(lowerTorso.Rotation, Is.EqualTo(Quaternion.identity));

                var upperTorso = result.Single(item => item.Bone == shortLeaf);
                Assert.That(upperTorso.Position.y, Is.EqualTo(0.06f).Within(0.0001f));
                Assert.That(upperTorso.Radius, Is.EqualTo(0.128f).Within(0.0001f));
                Assert.That(upperTorso.Height, Is.EqualTo(0.256f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(avatar);
            }
        }

        [Test]
        public void Create_PresetsBalanceColliderCountAndBoneCoverage()
        {
            var avatar = new GameObject("Avatar");
            try
            {
                var bones = new List<Transform>();
                var armature = AddBone(avatar.transform, "Armature", Vector3.zero, bones);
                var hips = AddBone(armature, "Hips", Vector3.zero, bones);
                var spine = AddBone(hips, "Spine", Vector3.up * 0.1f, bones);
                var chest = AddBone(spine, "Chest", Vector3.up * 0.1f, bones);
                var neck = AddBone(chest, "Neck", Vector3.up * 0.15f, bones);
                AddBone(neck, "Head", Vector3.up * 0.15f, bones);

                AddArm(chest, "Left", -1f, bones);
                AddArm(chest, "Right", 1f, bones);
                AddLeg(hips, "Left", -1f, bones);
                AddLeg(hips, "Right", 1f, bones);
                var auxiliary = AddBone(
                    chest,
                    "BreastAux",
                    Vector3.forward * 0.1f,
                    bones);
                AddBone(
                    auxiliary,
                    "BreastAuxEnd",
                    Vector3.forward * 0.1f,
                    bones);

                avatar.AddComponent<SkinnedMeshRenderer>().bones = bones.ToArray();

                var lightweight = BoneColliderLayout.Create(
                    avatar,
                    0.05f,
                    ColliderLayoutPreset.Lightweight);
                var standard = BoneColliderLayout.Create(
                    avatar,
                    0.05f,
                    ColliderLayoutPreset.Standard);
                var full = BoneColliderLayout.Create(
                    avatar,
                    0.05f,
                    ColliderLayoutPreset.Full);

                Assert.That(lightweight.Count, Is.EqualTo(7));
                Assert.That(standard.Count, Is.EqualTo(13));
                Assert.That(full.Count, Is.EqualTo(14));
                Assert.That(
                    lightweight.Single(item => item.Bone.name == "LeftUpperArm").TargetPath,
                    Does.EndWith("LeftHand"));
                Assert.That(
                    full.Any(item => item.Bone.name == "BreastAux"),
                    Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(avatar);
            }
        }

        [Test]
        public void Apply_ReplacesOwnedCollidersAndAssignsSelectedPhysBoneTransforms()
        {
            var avatar = new GameObject(
                "PhysBoneColliderGateway_" + Guid.NewGuid().ToString("N"));
            var paths = PhysBoneColliderGenerationPaths.Create(avatar);
            var generatedFolder = Path.GetDirectoryName(paths.PrefabPath)
                ?.Replace('\\', '/');
            try
            {
                var bone = new GameObject("Bone").transform;
                bone.SetParent(avatar.transform, false);
                var physBone = avatar.AddComponent<FakePhysBone>();
                var ignoredPhysBoneObject = new GameObject("IgnoredPhysBone");
                ignoredPhysBoneObject.transform.SetParent(avatar.transform, false);
                var ignoredPhysBone = ignoredPhysBoneObject.AddComponent<FakePhysBone>();
                var ignoredPhysBoneChild = new GameObject("Child").transform;
                ignoredPhysBoneChild.SetParent(ignoredPhysBoneObject.transform, false);
                var detectionRoot = new GameObject("DetectionRoot");
                detectionRoot.transform.SetParent(avatar.transform, false);
                var detectionArmature = new GameObject("Armature").transform;
                detectionArmature.SetParent(detectionRoot.transform, false);
                var detectedPhysBoneObject = new GameObject("DetectedPhysBone");
                detectedPhysBoneObject.transform.SetParent(detectionArmature, false);
                detectedPhysBoneObject.AddComponent<FakePhysBone>();
                var detectedPhysBoneChild = new GameObject("Child").transform;
                detectedPhysBoneChild.SetParent(
                    detectedPhysBoneObject.transform,
                    false);
                var nestedRoot = new GameObject("Nested").transform;
                nestedRoot.SetParent(detectionRoot.transform, false);
                var nestedArmature = new GameObject("Armature").transform;
                nestedArmature.SetParent(nestedRoot, false);
                nestedArmature.gameObject.AddComponent<FakePhysBone>();
                var draft = new PhysBoneColliderDraft(
                    bone,
                    "Bone",
                    0.2f,
                    new Vector3(0f, 0.05f, 0f),
                    Quaternion.Euler(0f, 0f, 90f),
                    0.06f,
                    0.2f);
                draft.AssignedPhysBonePaths.Add(string.Empty);
                var gateway = new VrchatPhysBoneColliderGateway(
                    typeof(FakePhysBoneCollider),
                    typeof(FakePhysBone),
                    typeof(FakeBoneProxy));

                var scopedTargets = gateway.FindPhysBoneTargets(
                    avatar,
                    new[] { detectionRoot });
                Assert.That(scopedTargets.Count, Is.EqualTo(1));
                Assert.That(
                    scopedTargets[0].Path,
                    Is.EqualTo("DetectionRoot/Armature/DetectedPhysBone"));
                Assert.That(
                    scopedTargets[0].TransformPaths,
                    Is.EquivalentTo(new[]
                    {
                        "DetectionRoot/Armature/DetectedPhysBone",
                        "DetectionRoot/Armature/DetectedPhysBone/Child"
                    }));
                var combinedTargets = gateway.FindPhysBoneTargets(
                    avatar,
                    new[] { detectionRoot, detectionRoot });
                Assert.That(combinedTargets.Count, Is.EqualTo(1));
                Assert.That(
                    gateway.FindPhysBoneTargets(avatar, new[] { avatar }),
                    Is.Empty);

                Assert.That(gateway.TryApply(
                    avatar,
                    new[] { draft },
                    out var firstCount,
                    out var firstError), Is.True, firstError);
                Assert.That(firstCount, Is.EqualTo(1));
                Assert.That(physBone.colliders.Count, Is.EqualTo(1));
                Assert.That(ignoredPhysBone.colliders, Is.Empty);
                var root = avatar.transform.Find(paths.RootName);
                Assert.That(root, Is.Not.Null);
                Assert.That(root.parent, Is.SameAs(avatar.transform));
                Assert.That(PrefabUtility.IsAnyPrefabInstanceRoot(root.gameObject), Is.True);
                Assert.That(
                    AssetDatabase.LoadAssetAtPath<GameObject>(paths.PrefabPath),
                    Is.Not.Null);
                Assert.That(physBone.colliders[0].transform.parent, Is.SameAs(root));
                Assert.That(
                    physBone.colliders[0].rootTransform,
                    Is.SameAs(physBone.colliders[0].transform));
                Assert.That(physBone.colliders[0].shapeType, Is.EqualTo(FakeShape.Capsule));
                Assert.That(physBone.colliders[0].position, Is.EqualTo(draft.Position));
                Assert.That(physBone.colliders[0].radius, Is.EqualTo(0.06f));
                Assert.That(physBone.colliders[0].height, Is.EqualTo(0.2f));
                Assert.That(physBone.colliders[0].rotation, Is.EqualTo(draft.Rotation));
                var proxy = physBone.colliders[0].GetComponent<FakeBoneProxy>();
                Assert.That(proxy, Is.Not.Null);
                Assert.That(proxy.boneReference, Is.EqualTo(FakeHumanBone.LastBone));
                Assert.That(proxy.subPath, Is.EqualTo("Bone"));
                Assert.That(proxy.attachmentMode, Is.EqualTo(FakeAttachment.AsChildAtRoot));
                Assert.That(proxy.matchScale, Is.True);

                draft.Radius = 0.01f;
                draft.AssignedPhysBonePaths.Clear();
                draft.AssignedPhysBonePaths.Add("IgnoredPhysBone");
                gateway.LoadOwned(avatar, new[] { draft });
                Assert.That(draft.Radius, Is.EqualTo(0.06f));
                Assert.That(draft.AssignmentsLoaded, Is.True);
                Assert.That(draft.AssignedPhysBonePaths, Is.EquivalentTo(new[] { string.Empty }));

                var previous = physBone.colliders[0];
                draft.Radius = 0.1f;
                draft.AssignedPhysBonePaths.Clear();
                draft.AssignedPhysBonePaths.Add("IgnoredPhysBone");
                Assert.That(gateway.TryApply(
                    avatar,
                    new[] { draft },
                    out var secondCount,
                    out var secondError), Is.True, secondError);

                Assert.That(secondCount, Is.EqualTo(1));
                Assert.That(physBone.colliders, Is.Empty);
                Assert.That(ignoredPhysBone.colliders.Count, Is.EqualTo(1));
                Assert.That(ignoredPhysBone.colliders[0], Is.Not.SameAs(previous));
                Assert.That(ignoredPhysBone.colliders[0].radius, Is.EqualTo(0.1f));
                Assert.That(
                    avatar.GetComponentsInChildren<FakePhysBoneCollider>(true).Length,
                    Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(avatar);
                if (!string.IsNullOrEmpty(generatedFolder))
                {
                    AssetDatabase.DeleteAsset(generatedFolder);
                }
            }
        }

        private static void AddArm(
            Transform chest,
            string side,
            float direction,
            ICollection<Transform> bones)
        {
            var shoulder = AddBone(
                chest,
                side + "Shoulder",
                Vector3.right * direction * 0.12f,
                bones);
            var upper = AddBone(
                shoulder,
                side + "UpperArm",
                Vector3.right * direction * 0.12f,
                bones);
            var lower = AddBone(
                upper,
                side + "LowerArm",
                Vector3.right * direction * 0.2f,
                bones);
            AddBone(
                lower,
                side + "Hand",
                Vector3.right * direction * 0.2f,
                bones);
        }

        private static void AddLeg(
            Transform hips,
            string side,
            float direction,
            ICollection<Transform> bones)
        {
            var upper = AddBone(
                hips,
                side + "UpperLeg",
                new Vector3(direction * 0.1f, -0.05f, 0f),
                bones);
            var lower = AddBone(
                upper,
                side + "LowerLeg",
                Vector3.down * 0.25f,
                bones);
            AddBone(
                lower,
                side + "Foot",
                new Vector3(0f, -0.25f, 0.08f),
                bones);
        }

        private static Transform AddBone(
            Transform parent,
            string name,
            Vector3 localPosition,
            ICollection<Transform> bones)
        {
            var bone = new GameObject(name).transform;
            bone.SetParent(parent, false);
            bone.localPosition = localPosition;
            bones.Add(bone);
            return bone;
        }

        private enum FakeShape
        {
            Sphere,
            Capsule
        }

        private sealed class FakePhysBoneCollider : MonoBehaviour
        {
            public Transform rootTransform;
            public FakeShape shapeType;
            public float radius;
            public float height;
            public Vector3 position;
            public Quaternion rotation;
        }

        private sealed class FakePhysBone : MonoBehaviour
        {
            public List<FakePhysBoneCollider> colliders =
                new List<FakePhysBoneCollider>();
        }

        private enum FakeHumanBone
        {
            Hips,
            LastBone
        }

        private enum FakeAttachment
        {
            Unset,
            AsChildAtRoot
        }

        private sealed class FakeBoneProxy : MonoBehaviour
        {
            public FakeHumanBone boneReference;
            public string subPath;
            public FakeAttachment attachmentMode;
            public bool matchScale;
        }
    }
}
