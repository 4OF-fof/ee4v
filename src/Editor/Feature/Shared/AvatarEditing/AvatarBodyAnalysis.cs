using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.EditorIntegration;
using Ee4v.Core.I18n;
using Ee4v.UI;
using Ee4v.AvatarEditing;
using static Ee4v.AvatarEditing.AvatarBodyAnalysis;
using static Ee4v.AvatarEditing.AvatarEditingUi;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Ee4v.AvatarEditing
{
    public static class AvatarBodyAnalysis
    {
        public static BodyPartCategory ClassifyBodyPart(
            string shapeName,
            string rendererName)
        {
            var normalized = NormalizeBlendShapeName(shapeName) +
                             NormalizeBlendShapeName(rendererName);
            if (ContainsBlendShapeTerm(normalized, HeadBlendShapeTerms))
            {
                return BodyPartCategory.Head;
            }
            if (ContainsBlendShapeTerm(normalized, ChestBlendShapeTerms))
            {
                return BodyPartCategory.Chest;
            }
            if (ContainsBlendShapeTerm(normalized, WaistBlendShapeTerms))
            {
                return BodyPartCategory.Waist;
            }
            if (ContainsBlendShapeTerm(
                    normalized,
                    ShoulderBlendShapeTerms))
            {
                return BodyPartCategory.Shoulders;
            }
            if (ContainsBlendShapeTerm(normalized, HandBlendShapeTerms))
            {
                return BodyPartCategory.Hands;
            }
            if (ContainsBlendShapeTerm(normalized, ArmBlendShapeTerms))
            {
                return BodyPartCategory.Arms;
            }
            if (ContainsBlendShapeTerm(normalized, FootBlendShapeTerms))
            {
                return BodyPartCategory.Feet;
            }
            if (ContainsBlendShapeTerm(normalized, LegBlendShapeTerms))
            {
                return BodyPartCategory.Legs;
            }
            return BodyPartCategory.Other;
        }

        public static string GetBodyPartCategoryLocalizationKey(
            BodyPartCategory category)
        {
            switch (category)
            {
                case BodyPartCategory.Head:
                    return "workflow.appearance.bodyShapePart.head";
                case BodyPartCategory.Chest:
                    return "workflow.appearance.bodyShapePart.chest";
                case BodyPartCategory.Waist:
                    return "workflow.appearance.bodyShapePart.waist";
                case BodyPartCategory.Shoulders:
                    return "workflow.appearance.bodyShapePart.shoulders";
                case BodyPartCategory.Arms:
                    return "workflow.appearance.bodyShapePart.arms";
                case BodyPartCategory.Hands:
                    return "workflow.appearance.bodyShapePart.hands";
                case BodyPartCategory.Legs:
                    return "workflow.appearance.bodyShapePart.legs";
                case BodyPartCategory.Feet:
                    return "workflow.appearance.bodyShapePart.feet";
                default:
                    return "workflow.appearance.bodyShapePart.other";
            }
        }

        private static bool ContainsBlendShapeTerm(
            string normalized,
            IEnumerable<string> terms)
        {
            if (string.IsNullOrEmpty(normalized))
            {
                return false;
            }

            return terms.Any(term => normalized.IndexOf(
                NormalizeBlendShapeName(term),
                StringComparison.Ordinal) >= 0);
        }

        public static IReadOnlyDictionary<Transform, BodyPartCategory>
            GetHumanoidMaterialBoneCategories(Animator animator)
        {
            var categories = new Dictionary<Transform, BodyPartCategory>();
            if (animator == null)
            {
                return categories;
            }

            Add(HumanBodyBones.Head, BodyPartCategory.Head);
            Add(HumanBodyBones.Neck, BodyPartCategory.Head);
            Add(HumanBodyBones.UpperChest, BodyPartCategory.Chest);
            Add(HumanBodyBones.Chest, BodyPartCategory.Chest);
            Add(HumanBodyBones.Spine, BodyPartCategory.Waist);
            Add(HumanBodyBones.Hips, BodyPartCategory.Waist);
            Add(HumanBodyBones.LeftShoulder, BodyPartCategory.Shoulders);
            Add(HumanBodyBones.RightShoulder, BodyPartCategory.Shoulders);
            Add(HumanBodyBones.LeftUpperArm, BodyPartCategory.Arms);
            Add(HumanBodyBones.RightUpperArm, BodyPartCategory.Arms);
            Add(HumanBodyBones.LeftLowerArm, BodyPartCategory.Arms);
            Add(HumanBodyBones.RightLowerArm, BodyPartCategory.Arms);
            Add(HumanBodyBones.LeftHand, BodyPartCategory.Hands);
            Add(HumanBodyBones.RightHand, BodyPartCategory.Hands);
            Add(HumanBodyBones.LeftUpperLeg, BodyPartCategory.Legs);
            Add(HumanBodyBones.RightUpperLeg, BodyPartCategory.Legs);
            Add(HumanBodyBones.LeftLowerLeg, BodyPartCategory.Legs);
            Add(HumanBodyBones.RightLowerLeg, BodyPartCategory.Legs);
            Add(HumanBodyBones.LeftFoot, BodyPartCategory.Feet);
            Add(HumanBodyBones.RightFoot, BodyPartCategory.Feet);
            Add(HumanBodyBones.LeftToes, BodyPartCategory.Feet);
            Add(HumanBodyBones.RightToes, BodyPartCategory.Feet);
            return categories;

            void Add(HumanBodyBones bone, BodyPartCategory category)
            {
                var transform = animator.GetBoneTransform(bone);
                if (transform != null)
                {
                    categories[transform] = category;
                }
            }
        }

        public static BodyPartCategory GetMaterialBoneCategory(
            Transform bone,
            IReadOnlyDictionary<Transform, BodyPartCategory> humanoidCategories)
        {
            for (var current = bone; current != null;
                 current = current.parent)
            {
                if (humanoidCategories.TryGetValue(
                        current, out var category))
                {
                    return category;
                }

                category = ClassifyBodyPart(current.name, string.Empty);
                if (category != BodyPartCategory.Other)
                {
                    return category;
                }
            }

            return BodyPartCategory.Other;
        }

        public static bool MatchesBodyPartGroup(
            BodyPartCategory selected,
            BodyPartCategory category)
        {
            return selected == category ||
                selected == BodyPartCategory.Shoulders &&
                category == BodyPartCategory.Arms;
        }

        public static IReadOnlyCollection<BodyPartCategory> GetMeshBodyPartCategories(
            GameObject root, Func<Transform, bool> isInScope)
        {
            var result = new HashSet<BodyPartCategory>();
            if (root == null) { return result; }
            var humanoidBones = new Dictionary<Transform, BodyPartCategory>();
            foreach (var animator in root.GetComponentsInChildren<Animator>(true))
            {
                if (animator.avatar == null || !animator.avatar.isValid || !animator.isHuman)
                {
                    continue;
                }
                foreach (var pair in GetHumanoidMaterialBoneCategories(animator))
                {
                    humanoidBones[pair.Key] = pair.Value;
                }
            }
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!isInScope(renderer.transform)) { continue; }
                var skinned = renderer as SkinnedMeshRenderer;
                var mesh = skinned != null ? skinned.sharedMesh
                    : renderer is MeshRenderer ? renderer.GetComponent<MeshFilter>()?.sharedMesh : null;
                if (mesh == null || mesh.vertexCount == 0 ||
                    !Enumerable.Range(0, mesh.subMeshCount).Any(slot => mesh.GetIndexCount(slot) > 0))
                {
                    continue;
                }
                if (skinned != null && TryGetWeightedMeshParts(skinned, humanoidBones, out var parts))
                {
                    result.UnionWith(parts);
                    continue;
                }
                var category = ClassifyBodyPart(renderer.name, mesh.name);
                if (category == BodyPartCategory.Other)
                {
                    category = GetMaterialBoneCategory(renderer.transform, humanoidBones);
                }
                if (category != BodyPartCategory.Other) { result.Add(category); }
            }
            return result;
        }

        private static bool TryGetWeightedMeshParts(SkinnedMeshRenderer renderer,
            IReadOnlyDictionary<Transform, BodyPartCategory> humanoidBones,
            out IReadOnlyCollection<BodyPartCategory> parts)
        {
            var result = new HashSet<BodyPartCategory>();
            parts = result;
            var mesh = renderer.sharedMesh;
            var bones = renderer.bones;
            if (bones == null || bones.Length == 0) { return false; }
            try
            {
                var weights = mesh.boneWeights;
                if (weights.Length != mesh.vertexCount) { return false; }
                var categories = bones.Select(bone => GetMaterialBoneCategory(bone, humanoidBones)).ToArray();
                var usedVertices = new HashSet<int>();
                for (var slot = 0; slot < mesh.subMeshCount; slot++)
                {
                    foreach (var vertex in mesh.GetIndices(slot))
                    {
                        if (vertex < 0 || vertex >= weights.Length || !usedVertices.Add(vertex)) { continue; }
                        var weight = weights[vertex];
                        var strongest = 0f;
                        var category = BodyPartCategory.Other;
                        Consider(weight.boneIndex0, weight.weight0);
                        Consider(weight.boneIndex1, weight.weight1);
                        Consider(weight.boneIndex2, weight.weight2);
                        Consider(weight.boneIndex3, weight.weight3);
                        if (category != BodyPartCategory.Other) { result.Add(category); }

                        void Consider(int index, float amount)
                        {
                            if (amount <= strongest || index < 0 || index >= categories.Length) { return; }
                            strongest = amount;
                            category = categories[index];
                        }
                    }
                }
                return result.Count > 0;
            }
            catch (UnityException)
            {
                return false;
            }
        }

        private static readonly string[] HeadBlendShapeTerms =
        {
            "head", "neck", "face", "facial", "hair", "ear",
            "eye", "brow", "頭", "首", "顔", "髪", "耳", "目", "眉"
        };

        private static readonly string[] ChestBlendShapeTerms =
        {
            "chest", "breast", "bust", "rib",
            "胸", "バスト", "乳"
        };

        private static readonly string[] WaistBlendShapeTerms =
        {
            "waist", "hip", "pelvis", "belly", "stomach", "abdomen",
            "torso", "body", "腰", "尻", "お尻", "腹", "お腹", "胴"
        };

        private static readonly string[] ShoulderBlendShapeTerms =
            { "shoulder", "肩" };

        private static readonly string[] ArmBlendShapeTerms =
            { "arm", "elbow", "腕", "肘" };

        private static readonly string[] HandBlendShapeTerms =
            { "hand", "finger", "wrist", "nail", "手", "指", "手首" };

        private static readonly string[] LegBlendShapeTerms =
        {
            "leg", "thigh", "calf", "knee", "shin",
            "脚", "太もも", "腿", "膝", "ふくらはぎ", "すね"
        };

        private static readonly string[] FootBlendShapeTerms =
            { "foot", "feet", "toe", "ankle", "足", "つま先", "足首" };
        public static string NormalizeBlendShapeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return new string(value
                .Trim()
                .ToLowerInvariant()
                .Where(character =>
                    !char.IsWhiteSpace(character) &&
                    character != '_' &&
                    character != '-' &&
                    character != '.')
                .ToArray());
        }
    }
}
