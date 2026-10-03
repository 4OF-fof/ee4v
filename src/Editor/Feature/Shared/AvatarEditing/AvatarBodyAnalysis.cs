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
