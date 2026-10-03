using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.EditorIntegration;
using static Ee4v.AvatarEditing.AvatarBodyAnalysis;
using UnityEditor;
using UnityEngine;

namespace Ee4v.AvatarParts
{
    public sealed partial class AvatarPartsEditor
    {
        private sealed class PrefabPartClassifier
        {
            private const float MinimumClassifiedWeightRatio = 0.65f;
            private const float MinimumPartWeightRatio = 0.1f;
            private static readonly IReadOnlyCollection<BodyPartCategory> Empty =
                Array.Empty<BodyPartCategory>();
            private readonly Dictionary<Transform, BodyPartCategory>
                _humanoidBones = new Dictionary<Transform, BodyPartCategory>();
            private readonly Dictionary<Transform,
                    IReadOnlyCollection<BodyPartCategory>>
                _objects = new Dictionary<Transform,
                    IReadOnlyCollection<BodyPartCategory>>();
            private readonly Dictionary<Transform, HashSet<BodyPartCategory>>
                _anchors = new Dictionary<Transform,
                    HashSet<BodyPartCategory>>();

            internal PrefabPartClassifier(GameObject root)
            {
                foreach (var animator in root.GetComponentsInChildren<Animator>(true))
                {
                    if (animator.avatar == null ||
                        !animator.avatar.isValid || !animator.isHuman)
                    {
                        continue;
                    }
                    foreach (var pair in GetHumanoidMaterialBoneCategories(animator))
                    {
                        _humanoidBones[pair.Key] = pair.Value;
                    }
                }
            }

            internal IReadOnlyCollection<BodyPartCategory> Classify(Transform target)
            {
                if (_objects.TryGetValue(target, out var cached))
                {
                    return cached;
                }

                var skinned = target.GetComponent<SkinnedMeshRenderer>();
                if (skinned != null && TryClassifySkin(skinned, out var skinParts))
                {
                    return _objects[target] = skinParts;
                }

                var anchors = GetAnchorCategories(target);
                if (anchors.Count > 0)
                {
                    if (anchors.Count == 1 &&
                        !anchors.Contains(BodyPartCategory.Other))
                    {
                        return _objects[target] = anchors.ToArray();
                    }
                    return _objects[target] = Empty;
                }

                var children = new HashSet<BodyPartCategory>();
                var hasRenderableChild = false;
                for (var index = 0; index < target.childCount; index++)
                {
                    var child = target.GetChild(index);
                    if (child.GetComponentInChildren<Renderer>(true) == null)
                    {
                        continue;
                    }
                    hasRenderableChild = true;
                    var childParts = Classify(child);
                    if (childParts.Count == 0)
                    {
                        return _objects[target] = Empty;
                    }
                    children.UnionWith(childParts);
                    if (children.Count > 2)
                    {
                        return _objects[target] = Empty;
                    }
                }
                if (hasRenderableChild)
                {
                    return _objects[target] = children.ToArray();
                }

                var named = ClassifyStructuralName(target.name);
                return _objects[target] = named == BodyPartCategory.Other
                    ? Empty
                    : (IReadOnlyCollection<BodyPartCategory>)new[] { named };
            }

            private bool TryClassifySkin(
                SkinnedMeshRenderer renderer,
                out IReadOnlyCollection<BodyPartCategory> categories)
            {
                categories = Empty;
                var mesh = renderer.sharedMesh;
                var bones = renderer.bones;
                if (mesh == null || bones == null || bones.Length == 0)
                {
                    return false;
                }

                BoneWeight[] weights;
                try
                {
                    weights = mesh.boneWeights;
                }
                catch (UnityException)
                {
                    return false;
                }
                if (weights == null || weights.Length != mesh.vertexCount)
                {
                    return false;
                }

                var boneParts = bones.Select(GetAnchorCategories)
                    .ToArray();
                var scores = new Dictionary<BodyPartCategory, float>();
                var totalWeight = 0f;
                var classifiedWeight = 0f;
                foreach (var weight in weights)
                {
                    Add(weight.boneIndex0, weight.weight0);
                    Add(weight.boneIndex1, weight.weight1);
                    Add(weight.boneIndex2, weight.weight2);
                    Add(weight.boneIndex3, weight.weight3);
                }

                if (totalWeight <= 0f ||
                    classifiedWeight < totalWeight *
                    MinimumClassifiedWeightRatio)
                {
                    return true;
                }

                var significant = scores
                    .Where(pair => pair.Value >= classifiedWeight *
                        MinimumPartWeightRatio)
                    .Select(pair => pair.Key)
                    .ToArray();
                if (significant.Length >= 1 && significant.Length <= 2)
                {
                    categories = significant;
                }
                return true;

                void Add(int boneIndex, float value)
                {
                    if (value <= 0f || boneIndex < 0 ||
                        boneIndex >= boneParts.Length)
                    {
                        return;
                    }
                    totalWeight += value;
                    var parts = boneParts[boneIndex];
                    if (parts.Count != 1 ||
                        parts.Contains(BodyPartCategory.Other))
                    {
                        return;
                    }
                    var part = parts.First();
                    scores.TryGetValue(part, out var score);
                    scores[part] = score + value;
                    classifiedWeight += value;
                }
            }

            private HashSet<BodyPartCategory> GetAnchorCategories(
                Transform target)
            {
                if (target == null)
                {
                    return new HashSet<BodyPartCategory>();
                }
                if (!_anchors.TryGetValue(target, out var categories))
                {
                    categories = ResolveAnchor(target,
                        new HashSet<Transform>());
                    _anchors.Add(target, categories);
                }
                return categories;
            }

            private HashSet<BodyPartCategory> ResolveAnchor(
                Transform target,
                HashSet<Transform> visiting)
            {
                var result = new HashSet<BodyPartCategory>();
                if (target == null || !visiting.Add(target))
                {
                    return result;
                }
                try
                {
                    if (_humanoidBones.TryGetValue(target, out var humanoid))
                    {
                        result.Add(NormalizePart(humanoid));
                        return result;
                    }

                    var hasConstraintSource = false;
                    foreach (var component in target.GetComponents<Component>())
                    {
                        if (component == null)
                        {
                            continue;
                        }
                        var typeName = component.GetType().Name;
                        if (typeName == "ModularAvatarBoneProxy")
                        {
                            var property = new SerializedObject(component)
                                .FindProperty("boneReference");
                            if (property != null)
                            {
                                var name = ((HumanBodyBones)property.intValue)
                                    .ToString();
                                var part = ClassifyStructuralName(name);
                                if (part != BodyPartCategory.Other)
                                {
                                    result.Add(part);
                                    return result;
                                }
                            }
                        }
                        if (typeName.IndexOf("Constraint",
                                StringComparison.Ordinal) < 0)
                        {
                            continue;
                        }
                        var properties = new SerializedObject(component)
                            .GetIterator();
                        while (properties.NextVisible(true))
                        {
                            if (properties.propertyType !=
                                    SerializedPropertyType.ObjectReference ||
                                properties.propertyPath.IndexOf("source",
                                    StringComparison.OrdinalIgnoreCase) < 0)
                            {
                                continue;
                            }
                            var source = properties.objectReferenceValue as Transform;
                            if (source == null &&
                                properties.objectReferenceValue is GameObject gameObject)
                            {
                                source = gameObject.transform;
                            }
                            if (source != null)
                            {
                                hasConstraintSource = true;
                                // Animation can activate a source whose current weight is zero.
                                var sourceParts = ResolveAnchor(source, visiting);
                                if (sourceParts.Count == 0)
                                {
                                    result.Add(BodyPartCategory.Other);
                                }
                                else
                                {
                                    result.UnionWith(sourceParts);
                                }
                            }
                        }
                    }
                    if (hasConstraintSource)
                    {
                        return result;
                    }
                    if (result.Count > 0)
                    {
                        return result;
                    }

                    var named = ClassifyStructuralName(target.name);
                    if (named != BodyPartCategory.Other)
                    {
                        result.Add(named);
                        return result;
                    }
                    return ResolveAnchor(target.parent, visiting);
                }
                finally
                {
                    visiting.Remove(target);
                }
            }

            private static BodyPartCategory NormalizePart(
                BodyPartCategory part)
            {
                return part == BodyPartCategory.Arms
                    ? BodyPartCategory.Shoulders
                    : part;
            }

            private static BodyPartCategory ClassifyStructuralName(string name)
            {
                if (string.IsNullOrEmpty(name) ||
                    name.Equals("Armature", StringComparison.OrdinalIgnoreCase) ||
                    name.StartsWith("Armature.",
                        StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("Root", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("Body", StringComparison.OrdinalIgnoreCase) ||
                    name.StartsWith("Body_", StringComparison.OrdinalIgnoreCase))
                {
                    return BodyPartCategory.Other;
                }
                var words = PrefabHierarchyUtility.SplitName(name);
                if (ContainsWord(words, "head", "neck", "face", "facial",
                        "hair", "ear", "eye", "brow", "goggle", "goggles",
                        "visor") || ContainsJapanese(name, "頭", "首", "顔", "髪", "耳"))
                {
                    return BodyPartCategory.Head;
                }
                if (ContainsWord(words, "chest", "breast", "bust", "bra",
                        "ribcage") ||
                    ContainsJapanese(name, "胸", "バスト", "乳"))
                {
                    return BodyPartCategory.Chest;
                }
                if (ContainsWord(words, "hand", "hands", "wrist", "finger",
                        "thumb", "index", "middle", "ring", "little",
                        "bracelet", "bracelets", "watch", "glove", "gloves") ||
                    ContainsJapanese(name, "手", "指"))
                {
                    return BodyPartCategory.Hands;
                }
                if (ContainsWord(words, "shoulder", "shoulders", "arm",
                        "arms", "forearm", "elbow") ||
                    ContainsJapanese(name, "肩", "腕", "肘"))
                {
                    return BodyPartCategory.Shoulders;
                }
                if (ContainsWord(words, "foot", "feet", "toe", "toes",
                        "ankle", "anklet", "boot", "boots", "sandal",
                        "sandals") ||
                    ContainsJapanese(name, "足", "つま先", "足首"))
                {
                    return BodyPartCategory.Feet;
                }
                if (ContainsWord(words, "leg", "legs", "thigh", "calf",
                        "knee", "shin", "tights") ||
                    ContainsJapanese(name, "脚", "腿", "膝"))
                {
                    return BodyPartCategory.Legs;
                }
                if (ContainsWord(words, "waist", "hip", "hips", "pelvis",
                        "spine", "belly", "abdomen", "skirt", "shorts",
                        "belt", "tail") ||
                    ContainsJapanese(name, "腰", "尻", "腹", "胴"))
                {
                    return BodyPartCategory.Waist;
                }
                return BodyPartCategory.Other;
            }

            private static bool ContainsWord(
                IReadOnlyCollection<string> words,
                params string[] terms)
            {
                return words.Any(word => terms.Contains(word));
            }

            private static bool ContainsJapanese(
                string name,
                params string[] terms)
            {
                return terms.Any(term => name.IndexOf(
                    term, StringComparison.Ordinal) >= 0);
            }
        }
    }
}
