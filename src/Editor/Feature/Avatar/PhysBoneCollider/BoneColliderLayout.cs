using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Ee4v.PhysBoneCollider
{
    internal sealed class PhysBoneColliderDraft
    {
        internal PhysBoneColliderDraft(
            Transform bone,
            string path,
            float boneLength,
            Vector3 position,
            Quaternion rotation,
            float radius,
            float height)
        {
            Bone = bone;
            Path = path;
            BoneLength = boneLength;
            SuggestedPosition = position;
            SuggestedRotation = rotation;
            SuggestedRadius = radius;
            SuggestedHeight = height;
            Enabled = true;
            Position = position;
            Rotation = rotation;
            Radius = radius;
            Height = height;
        }

        internal Transform Bone { get; }

        internal string Path { get; }

        internal float BoneLength { get; }

        internal Vector3 SuggestedPosition { get; }

        internal Quaternion SuggestedRotation { get; }

        internal float SuggestedRadius { get; }

        internal float SuggestedHeight { get; }

        internal bool Enabled { get; set; }

        internal Vector3 Position { get; set; }

        internal Quaternion Rotation { get; set; }

        internal float Radius { get; set; }

        internal float Height { get; set; }

        internal void Reset()
        {
            Enabled = true;
            Position = SuggestedPosition;
            Rotation = SuggestedRotation;
            Radius = SuggestedRadius;
            Height = SuggestedHeight;
        }
    }

    internal static class BoneColliderLayout
    {
        private static readonly string[] EditorOnlyInterfaceNames =
        {
            "VRC.SDKBase.IEditorOnly",
            "nadena.dev.ndmf.INDMFEditorOnly",
            "nadena.dev.modular_avatar.core.IEditorOnly"
        };
        private const float PositionRatio = 0.5f;
        private const float RadiusRatio = 0.18f;
        private const float MaximumRadius = 0.25f;
        private static readonly HumanBodyBones[] MajorHumanoidBones =
        {
            HumanBodyBones.Hips,
            HumanBodyBones.Spine,
            HumanBodyBones.Chest,
            HumanBodyBones.UpperChest,
            HumanBodyBones.Neck,
            HumanBodyBones.Head,
            HumanBodyBones.LeftShoulder,
            HumanBodyBones.LeftUpperArm,
            HumanBodyBones.LeftLowerArm,
            HumanBodyBones.LeftHand,
            HumanBodyBones.RightShoulder,
            HumanBodyBones.RightUpperArm,
            HumanBodyBones.RightLowerArm,
            HumanBodyBones.RightHand,
            HumanBodyBones.LeftUpperLeg,
            HumanBodyBones.LeftLowerLeg,
            HumanBodyBones.LeftFoot,
            HumanBodyBones.RightUpperLeg,
            HumanBodyBones.RightLowerLeg,
            HumanBodyBones.RightFoot
        };
        private static readonly string[] ExcludedBoneNames =
        {
            "eye", "jaw", "finger", "thumb", "index", "middle",
            "ring", "little", "pinky", "toe", "end"
        };
        private static readonly string[] BodyBoneNames =
        {
            "hips", "pelvis", "spine", "chest", "neck", "head",
            "shoulder", "upperarm", "lowerarm", "forearm", "leftarm",
            "rightarm", "elbow", "wrist", "hand", "upperleg",
            "lowerleg", "leftleg", "rightleg", "thigh", "calf",
            "knee", "ankle", "foot"
        };

        internal static IReadOnlyList<PhysBoneColliderDraft> Create(
            GameObject avatar,
            float minimumBoneLength)
        {
            if (avatar == null)
            {
                return Array.Empty<PhysBoneColliderDraft>();
            }

            var root = avatar.transform;
            var armature = FindArmature(avatar);
            if (armature == null)
            {
                return Array.Empty<PhysBoneColliderDraft>();
            }

            var deformBones = new HashSet<Transform>();
            foreach (var renderer in avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                foreach (var bone in renderer.bones)
                {
                    if (bone != null &&
                        bone != armature &&
                        bone.IsChildOf(armature) &&
                        !IsEditorOnly(bone, armature))
                    {
                        deformBones.Add(bone);
                    }
                }
            }

            var bones = FindBodyBones(avatar, deformBones, armature);
            var threshold = Mathf.Max(0.001f, minimumBoneLength);
            var drafts = new List<PhysBoneColliderDraft>();
            var torsoBones = AddTorsoDrafts(
                avatar,
                bones,
                root,
                threshold,
                drafts);
            foreach (var bone in bones)
            {
                if (torsoBones.Contains(bone))
                {
                    continue;
                }

                var child = FindConnectedBone(bone, bones);
                if (child == null)
                {
                    continue;
                }

                var worldDelta = child.position - bone.position;
                var length = worldDelta.magnitude;
                if (length < threshold)
                {
                    continue;
                }

                var radius = Mathf.Min(MaximumRadius, length * RadiusRatio);
                drafts.Add(CreateDraft(bone, child, root, radius, length));
            }

            return drafts
                .OrderBy(draft => draft.Path, StringComparer.Ordinal)
                .ToArray();
        }

        private static HashSet<Transform> AddTorsoDrafts(
            GameObject avatar,
            ISet<Transform> bones,
            Transform root,
            float threshold,
            ICollection<PhysBoneColliderDraft> drafts)
        {
            var animator = avatar.GetComponent<Animator>();
            var isHumanoid = animator != null && animator.isHuman;
            var hips = isHumanoid
                ? FindHumanoidBone(animator, bones, HumanBodyBones.Hips)
                : FindNamedBone(bones, "hips", "pelvis");
            var chest = isHumanoid
                ? FindHumanoidBone(animator, bones, HumanBodyBones.Chest) ??
                  FindHumanoidBone(animator, bones, HumanBodyBones.UpperChest)
                : FindNamedBone(bones, "chest");
            var neck = isHumanoid
                ? FindHumanoidBone(animator, bones, HumanBodyBones.Neck) ??
                  FindHumanoidBone(animator, bones, HumanBodyBones.Head)
                : FindNamedBone(bones, "neck", "head");
            var leftUpperLeg = isHumanoid
                ? FindHumanoidBone(animator, bones, HumanBodyBones.LeftUpperLeg)
                : FindNamedBone(bones, "leftupperleg", "leftthigh");
            var rightUpperLeg = isHumanoid
                ? FindHumanoidBone(animator, bones, HumanBodyBones.RightUpperLeg)
                : FindNamedBone(bones, "rightupperleg", "rightthigh");
            var leftUpperArm = isHumanoid
                ? FindHumanoidBone(animator, bones, HumanBodyBones.LeftUpperArm)
                : FindNamedBone(bones, "leftupperarm", "leftarm");
            var rightUpperArm = isHumanoid
                ? FindHumanoidBone(animator, bones, HumanBodyBones.RightUpperArm)
                : FindNamedBone(bones, "rightupperarm", "rightarm");

            var before = drafts.Count;
            var lowerAdded = TryAddTorsoDraft(
                hips,
                chest,
                root,
                threshold,
                Distance(leftUpperLeg, rightUpperLeg),
                0.55f,
                0.28f,
                drafts);
            var mergedAdded = false;
            if (!lowerAdded && hips != null && neck != null)
            {
                mergedAdded = TryAddTorsoDraft(
                    hips,
                    neck,
                    root,
                    threshold,
                    Mathf.Max(
                        Distance(leftUpperLeg, rightUpperLeg),
                        Distance(leftUpperArm, rightUpperArm)),
                    0.4f,
                    0.3f,
                    drafts);
            }

            if (!mergedAdded)
            {
                TryAddTorsoDraft(
                    chest,
                    neck,
                    root,
                    threshold,
                    Distance(leftUpperArm, rightUpperArm),
                    0.32f,
                    0.3f,
                    drafts);
            }

            if (drafts.Count == before)
            {
                return new HashSet<Transform>();
            }

            var suppressed = new HashSet<Transform>(
                bones.Where(IsTorsoBoneName));
            if (isHumanoid)
            {
                foreach (var bodyBone in new[]
                         {
                             HumanBodyBones.Hips,
                             HumanBodyBones.Spine,
                             HumanBodyBones.Chest,
                             HumanBodyBones.UpperChest
                         })
                {
                    var transform = FindHumanoidBone(animator, bones, bodyBone);
                    if (transform != null)
                    {
                        suppressed.Add(transform);
                    }
                }
            }

            return suppressed;
        }

        private static bool TryAddTorsoDraft(
            Transform bone,
            Transform target,
            Transform root,
            float threshold,
            float bodyWidth,
            float widthRatio,
            float lengthRatio,
            ICollection<PhysBoneColliderDraft> drafts)
        {
            if (bone == null || target == null || !target.IsChildOf(bone))
            {
                return false;
            }

            var length = Vector3.Distance(bone.position, target.position);
            if (length < threshold)
            {
                return false;
            }

            var radius = Mathf.Min(
                MaximumRadius,
                Mathf.Max(length * lengthRatio, bodyWidth * widthRatio));
            drafts.Add(CreateDraft(
                bone,
                target,
                root,
                radius,
                Mathf.Max(length, radius * 2f)));
            return true;
        }

        private static PhysBoneColliderDraft CreateDraft(
            Transform bone,
            Transform target,
            Transform root,
            float radius,
            float height)
        {
            var worldDelta = target.position - bone.position;
            var length = worldDelta.magnitude;
            var localPosition = bone.InverseTransformVector(
                worldDelta * PositionRatio);
            var localDirection = bone.InverseTransformDirection(
                worldDelta.normalized);
            var localRotation = Quaternion.FromToRotation(
                Vector3.up,
                localDirection);
            return new PhysBoneColliderDraft(
                bone,
                AnimationUtility.CalculateTransformPath(bone, root),
                length,
                localPosition,
                localRotation,
                radius,
                height);
        }

        private static Transform FindHumanoidBone(
            Animator animator,
            ISet<Transform> bones,
            HumanBodyBones bodyBone)
        {
            var transform = animator.GetBoneTransform(bodyBone);
            return transform != null && bones.Contains(transform)
                ? transform
                : null;
        }

        private static Transform FindNamedBone(
            IEnumerable<Transform> bones,
            params string[] names)
        {
            foreach (var name in names)
            {
                var match = bones
                    .Where(bone => NormalizeBoneName(bone.name).Contains(name))
                    .OrderBy(bone => NormalizeBoneName(bone.name).Length)
                    .FirstOrDefault();
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static float Distance(Transform left, Transform right)
        {
            return left != null && right != null
                ? Vector3.Distance(left.position, right.position)
                : 0f;
        }

        private static bool IsTorsoBoneName(Transform bone)
        {
            var name = NormalizeBoneName(bone.name);
            return name.Contains("hips") ||
                   name.Contains("pelvis") ||
                   name.Contains("spine") ||
                   name.Contains("chest");
        }

        private static HashSet<Transform> FindBodyBones(
            GameObject avatar,
            ISet<Transform> deformBones,
            Transform armature)
        {
            var result = new HashSet<Transform>();
            var animator = avatar.GetComponent<Animator>();
            if (animator != null && animator.isHuman)
            {
                foreach (var bodyBone in MajorHumanoidBones)
                {
                    var transform = animator.GetBoneTransform(bodyBone);
                    if (transform != null &&
                        deformBones.Contains(transform) &&
                        !IsEditorOnly(transform, armature))
                    {
                        result.Add(transform);
                    }
                }

                return result;
            }

            foreach (var bone in deformBones)
            {
                if (IsBodyBoneName(bone.name))
                {
                    result.Add(bone);
                }
            }

            return result;
        }

        private static bool IsBodyBoneName(string boneName)
        {
            var normalized = NormalizeBoneName(boneName);
            if (ExcludedBoneNames.Any(normalized.Contains))
            {
                return false;
            }

            return BodyBoneNames.Any(normalized.Contains);
        }

        private static string NormalizeBoneName(string boneName)
        {
            return new string((boneName ?? string.Empty)
                .Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant)
                .ToArray());
        }

        private static bool IsEditorOnly(Transform bone, Transform armature)
        {
            for (var current = bone; current != null; current = current.parent)
            {
                if (current.CompareTag("EditorOnly") || HasEditorOnlyComponent(current))
                {
                    return true;
                }

                if (current == armature)
                {
                    break;
                }
            }

            return false;
        }

        private static bool HasEditorOnlyComponent(Transform transform)
        {
            foreach (var component in transform.GetComponents<Component>())
            {
                if (component == null)
                {
                    continue;
                }

                if (component.GetType().GetInterfaces().Any(type =>
                        EditorOnlyInterfaceNames.Contains(type.FullName)))
                {
                    return true;
                }
            }

            return false;
        }

        internal static Transform FindArmature(GameObject avatar)
        {
            if (avatar == null)
            {
                return null;
            }

            var root = avatar.transform;
            for (var index = 0; index < root.childCount; index++)
            {
                var child = root.GetChild(index);
                if (string.Equals(
                        child.name,
                        "Armature",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return child;
                }
            }

            return null;
        }

        private static Transform FindConnectedBone(
            Transform bone,
            ISet<Transform> bones)
        {
            var connected = new List<Transform>();
            for (var index = 0; index < bone.childCount; index++)
            {
                CollectFirstBones(bone.GetChild(index), bones, connected);
            }

            return connected
                .OrderBy(candidate => Vector3.Distance(
                    bone.position,
                    candidate.position))
                .FirstOrDefault();
        }

        private static void CollectFirstBones(
            Transform current,
            ISet<Transform> bones,
            ICollection<Transform> result)
        {
            if (bones.Contains(current))
            {
                result.Add(current);
                return;
            }

            for (var index = 0; index < current.childCount; index++)
            {
                CollectFirstBones(current.GetChild(index), bones, result);
            }
        }
    }
}
