using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.EditorIntegration;
using Ee4v.Core.I18n;
using static Ee4v.AvatarEditing.AvatarBodyAnalysis;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEngine;

namespace Ee4v.AvatarParts
{
    public sealed partial class AvatarPartsEditor
    {
        private IReadOnlyList<BodyBlendShapeDefinition>
            GetBodyBlendShapes()
        {
            if (_context.Root == null)
            {
                return Array.Empty<BodyBlendShapeDefinition>();
            }

            if (_bodyBlendShapesCache != null)
            {
                foreach (var definition in _bodyBlendShapesCache)
                {
                    var renderer = GetBodyBlendShapeRenderer(definition);
                    definition.Value = GetEffectiveBodyBlendShapeWeight(
                        renderer, definition.ShapeName);
                }
                return _bodyBlendShapesCache;
            }

            _bodyBlendShapesCache = ReadBodyBlendShapes(false);
            return _bodyBlendShapesCache;
        }

        private IReadOnlyList<BodyBlendShapeDefinition> ReadBodyBlendShapes(
            bool includeAllPrefabs)
        {
            var renderers = _context.Root
                .GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(renderer =>
                    renderer != null &&
                    (includeAllPrefabs ||
                     _context.IsInSelectedPrefabScope(renderer.transform)) &&
                    renderer.sharedMesh != null &&
                    renderer.sharedMesh.blendShapeCount > 0)
                .ToArray();
            if (renderers.Length == 0)
            {
                return Array.Empty<BodyBlendShapeDefinition>();
            }

            var naming = _context.Host.CreateShapeNaming(
                _context.Root,
                renderers.Select(renderer => renderer.sharedMesh));
            var result = new List<BodyBlendShapeDefinition>();
            foreach (var renderer in renderers)
            {
                var rendererPath = AnimationUtility.CalculateTransformPath(
                    renderer.transform,
                    _context.Root.transform);
                var meshAssetPath = AssetDatabase.GetAssetPath(
                    renderer.sharedMesh);
                var sourceAssetGuid = string.Empty;
                var sourceMeshLocalId = 0L;
                if (string.Equals(
                        System.IO.Path.GetExtension(meshAssetPath),
                        ".fbx",
                        StringComparison.OrdinalIgnoreCase))
                {
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                        renderer.sharedMesh,
                        out sourceAssetGuid,
                        out sourceMeshLocalId);
                }
                for (var shapeIndex = 0;
                     shapeIndex < renderer.sharedMesh.blendShapeCount;
                     shapeIndex++)
                {
                    var shapeName = renderer.sharedMesh
                        .GetBlendShapeName(shapeIndex);
                    if (naming.IsHeader(shapeName))
                    {
                        continue;
                    }
                    naming.TryGetMapping(
                        sourceAssetGuid,
                        sourceMeshLocalId,
                        shapeName,
                        out var mapping);
                    if (string.Equals(
                            mapping.AppearancePart,
                            "expression",
                            StringComparison.Ordinal))
                    {
                        continue;
                    }
                    if (!TryGetPresetBodyPart(
                            mapping.AppearancePart,
                            out var category))
                    {
                        if (IsBodyMesh(renderer))
                        {
                            continue;
                        }
                        category = ClassifyBodyPart(
                            shapeName,
                            renderer.name);
                    }
                    result.Add(new BodyBlendShapeDefinition
                    {
                        RendererPath = rendererPath,
                        RendererDisplayPath = string.IsNullOrEmpty(rendererPath)
                            ? renderer.name
                            : rendererPath,
                        RendererName = renderer.name,
                        ShapeName = shapeName,
                        DisplayName = GetBodyBlendShapeDisplayName(shapeName),
                        Category = category,
                        Group = string.IsNullOrWhiteSpace(
                            mapping.AppearanceGroup)
                            ? mapping.Role?.Trim() ?? string.Empty
                            : mapping.AppearanceGroup.Trim(),
                        Value = GetEffectiveBodyBlendShapeWeight(
                            renderer, shapeName),
                        BaseValue = GetBaseBlendShapeWeight(
                            renderer,
                            shapeName)
                    });
                }
            }
            AddSyncedBodyBlendShapes(result);
            return result;
        }

        private void AddSyncedBodyBlendShapes(
            List<BodyBlendShapeDefinition> definitions)
        {
            var synced = definitions
                .Where(definition => ResolveBodyBlendShapeSource(
                    GetBodyBlendShapeRenderer(definition),
                    definition.ShapeName) != null)
                .GroupBy(definition => definition.ShapeName,
                    StringComparer.Ordinal)
                .ToArray();
            foreach (var group in synced)
            {
                var representative = group.First();
                foreach (var renderer in _context.Root
                             .GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (renderer?.sharedMesh == null ||
                        renderer.sharedMesh.GetBlendShapeIndex(group.Key) < 0)
                    {
                        continue;
                    }
                    var path = AnimationUtility.CalculateTransformPath(
                        renderer.transform, _context.Root.transform);
                    if (definitions.Any(definition =>
                            definition.RendererPath == path &&
                            definition.ShapeName == group.Key))
                    {
                        continue;
                    }
                    definitions.Add(new BodyBlendShapeDefinition
                    {
                        RendererPath = path,
                        RendererDisplayPath = path,
                        RendererName = renderer.name,
                        ShapeName = group.Key,
                        DisplayName = representative.DisplayName,
                        Category = representative.Category,
                        Group = representative.Group,
                        Value = GetEffectiveBodyBlendShapeWeight(
                            renderer, group.Key),
                        BaseValue = GetBaseBlendShapeWeight(renderer, group.Key)
                    });
                }
                var source = ResolveBodyBlendShapeGroupSource(group.ToArray());
                var sourceDefinition = definitions.FirstOrDefault(definition =>
                    definition.ShapeName == group.Key &&
                    GetBodyBlendShapeRenderer(definition) == source);
                var category = sourceDefinition == null
                    ? representative.Category
                    : sourceDefinition.Category;
                foreach (var definition in definitions.Where(definition =>
                             definition.ShapeName == group.Key))
                {
                    definition.Category = category;
                    definition.Group = representative.Group;
                }
            }
        }

        private SkinnedMeshRenderer GetBodyBlendShapeRenderer(
            BodyBlendShapeDefinition definition)
        {
            if (_context.Root == null || definition == null)
            {
                return null;
            }
            var target = string.IsNullOrEmpty(definition.RendererPath)
                ? _context.Root.transform
                : _context.Root.transform.Find(definition.RendererPath);
            return target == null
                ? null
                : target.GetComponent<SkinnedMeshRenderer>();
        }

        private SkinnedMeshRenderer ResolveBodyBlendShapeGroupSource(
            IReadOnlyList<BodyBlendShapeDefinition> definitions)
        {
            foreach (var definition in definitions)
            {
                var source = ResolveBodyBlendShapeSource(
                    GetBodyBlendShapeRenderer(definition),
                    definition.ShapeName);
                if (source != null)
                {
                    return source;
                }
            }
            return definitions.Count == 0
                ? null
                : GetBodyBlendShapeRenderer(definitions[0]);
        }

        private bool IsBodyBlendShapeGroupSource(
            BodyBlendShapeDefinition definition,
            SkinnedMeshRenderer source)
        {
            return source != null &&
                   GetBodyBlendShapeRenderer(definition) == source;
        }

        private SkinnedMeshRenderer ResolveBodyBlendShapeSource(
            SkinnedMeshRenderer target,
            string localShapeName)
        {
            if (target == null || _context.Root == null)
            {
                return null;
            }
            var sync = target.GetComponent<ModularAvatarBlendshapeSync>();
            if (sync?.Bindings == null)
            {
                return null;
            }
            foreach (var binding in sync.Bindings)
            {
                var localName = string.IsNullOrWhiteSpace(
                    binding.LocalBlendshape)
                    ? binding.Blendshape
                    : binding.LocalBlendshape;
                if (localName != localShapeName ||
                    binding.ReferenceMesh == null)
                {
                    continue;
                }
                var path = binding.ReferenceMesh.referencePath;
                GameObject sourceObject = null;
                if (!string.IsNullOrEmpty(path))
                {
                    var sourceTransform = path ==
                        AvatarObjectReference.AVATAR_ROOT
                        ? _context.Root.transform
                        : _context.Root.transform.Find(path);
                    sourceObject = sourceTransform == null
                        ? null
                        : sourceTransform.gameObject;
                }
                if (sourceObject == null)
                {
                    sourceObject = binding.ReferenceMesh.Get(sync);
                }
                var source = sourceObject == null
                    ? null
                    : sourceObject.GetComponent<SkinnedMeshRenderer>();
                if (source?.sharedMesh != null &&
                    source.sharedMesh.GetBlendShapeIndex(
                        binding.Blendshape) >= 0)
                {
                    return source;
                }
            }
            return null;
        }

        private static bool HasBodyBlendShapeSyncBinding(
            SkinnedMeshRenderer target,
            string shapeName)
        {
            var sync = target == null
                ? null
                : target.GetComponent<ModularAvatarBlendshapeSync>();
            return sync?.Bindings != null && sync.Bindings.Any(binding =>
                (string.IsNullOrWhiteSpace(binding.LocalBlendshape)
                    ? binding.Blendshape
                    : binding.LocalBlendshape) == shapeName);
        }

        private float GetEffectiveBodyBlendShapeWeight(
            SkinnedMeshRenderer renderer,
            string shapeName)
        {
            var index = renderer?.sharedMesh == null
                ? -1
                : renderer.sharedMesh.GetBlendShapeIndex(shapeName);
            if (index < 0)
            {
                return 0f;
            }
            var sync = renderer.GetComponent<ModularAvatarBlendshapeSync>();
            if (sync?.Bindings != null)
            {
                foreach (var binding in sync.Bindings)
                {
                    var localName = string.IsNullOrWhiteSpace(
                        binding.LocalBlendshape)
                        ? binding.Blendshape
                        : binding.LocalBlendshape;
                    if (localName != shapeName)
                    {
                        continue;
                    }
                    var source = ResolveBodyBlendShapeSource(
                        renderer, shapeName);
                    var sourceIndex = source?.sharedMesh == null
                        ? -1
                        : source.sharedMesh.GetBlendShapeIndex(
                            binding.Blendshape);
                    if (sourceIndex >= 0)
                    {
                        var weight = source.GetBlendShapeWeight(sourceIndex);
                        return Mathf.Clamp(
                            binding.RemapCurveIsValid
                                ? EvaluateBodyBlendShapeRemap(
                                    binding.RemapCurve, weight)
                                : weight,
                            MinimumBodyBlendShapeWeight,
                            MaximumBodyBlendShapeWeight);
                    }
                }
            }
            return Mathf.Clamp(renderer.GetBlendShapeWeight(index),
                MinimumBodyBlendShapeWeight,
                MaximumBodyBlendShapeWeight);
        }

        private static float EvaluateBodyBlendShapeRemap(
            AnimationCurve curve,
            float weight)
        {
            if (curve == null || curve.length < 2)
            {
                return weight;
            }
            var keys = curve.keys;
            for (var index = 1; index < keys.Length; index++)
            {
                if (weight > keys[index].time && index < keys.Length - 1)
                {
                    continue;
                }
                var previous = keys[index - 1];
                var next = keys[index];
                var duration = next.time - previous.time;
                return Mathf.Approximately(duration, 0f)
                    ? next.value
                    : Mathf.LerpUnclamped(previous.value, next.value,
                        (weight - previous.time) / duration);
            }
            return keys[keys.Length - 1].value;
        }

        private static bool TryGetPresetBodyPart(
            string part,
            out BodyPartCategory category)
        {
            switch (part)
            {
                case "head":
                    category = BodyPartCategory.Head;
                    break;
                case "chest":
                    category = BodyPartCategory.Chest;
                    break;
                case "waist":
                    category = BodyPartCategory.Waist;
                    break;
                case "shoulders":
                    category = BodyPartCategory.Shoulders;
                    break;
                case "arms":
                    category = BodyPartCategory.Arms;
                    break;
                case "hands":
                    category = BodyPartCategory.Hands;
                    break;
                case "legs":
                    category = BodyPartCategory.Legs;
                    break;
                case "feet":
                    category = BodyPartCategory.Feet;
                    break;
                case "other":
                    category = BodyPartCategory.Other;
                    break;
                default:
                    category = BodyPartCategory.Other;
                    return false;
            }

            return true;
        }

        private static string GetBodyBlendShapeDisplayName(string shapeName)
        {
            var name = (shapeName ?? string.Empty).Trim();
            var separator = name.IndexOf('/');
            if (separator < 0 || separator >= name.Length - 1)
            {
                return name;
            }

            var role = name.Substring(separator + 1).Trim();
            return role.Length > 0 ? role : name;
        }

        private static bool IsBodyMesh(SkinnedMeshRenderer renderer)
        {
            return renderer != null &&
                   (string.Equals(
                        NormalizeBlendShapeName(renderer.name),
                        "body",
                        StringComparison.Ordinal) ||
                    string.Equals(
                        NormalizeBlendShapeName(renderer.sharedMesh?.name),
                        "body",
                        StringComparison.Ordinal));
        }

        private static float GetBaseBlendShapeWeight(
            SkinnedMeshRenderer renderer,
            string shapeName)
        {
            var source = PrefabUtility
                .GetCorrespondingObjectFromSource(renderer) as
                SkinnedMeshRenderer;
            var index = source?.sharedMesh == null
                ? -1
                : source.sharedMesh.GetBlendShapeIndex(shapeName);
            return index < 0
                ? MinimumBodyBlendShapeWeight
                : Mathf.Clamp(
                    source.GetBlendShapeWeight(index),
                    MinimumBodyBlendShapeWeight,
                    MaximumBodyBlendShapeWeight);
        }

        private static bool TryGetAvatarViewPosition(
            GameObject avatar,
            out Component descriptor,
            out Vector3 viewPosition)
        {
            descriptor = avatar == null
                ? null
                : avatar.GetComponentsInChildren<Component>(true)
                    .FirstOrDefault(component =>
                        component != null &&
                        string.Equals(
                            component.GetType().FullName,
                            AvatarDescriptorTypeName,
                            StringComparison.Ordinal));
            return TryReadAvatarViewPosition(descriptor, out viewPosition) &&
                   viewPosition.y > 0.01f;
        }

        private bool TryGetWorkingAvatarViewPosition(
            out Component descriptor,
            out Vector3 viewPosition)
        {
            if (_avatarDescriptor != null &&
                TryReadAvatarViewPosition(
                    _avatarDescriptor,
                    out viewPosition) &&
                viewPosition.y > 0.01f)
            {
                descriptor = _avatarDescriptor;
                return true;
            }

            var found = TryGetAvatarViewPosition(
                _context.Root,
                out descriptor,
                out viewPosition);
            _avatarDescriptor = found ? descriptor : null;
            return found;
        }

        private static bool TryReadAvatarViewPosition(
            Component descriptor,
            out Vector3 viewPosition)
        {
            viewPosition = Vector3.zero;
            if (descriptor == null)
            {
                return false;
            }

            var serialized = new SerializedObject(descriptor);
            var property = serialized.FindProperty("ViewPosition");
            if (property == null ||
                property.propertyType != SerializedPropertyType.Vector3)
            {
                return false;
            }

            viewPosition = property.vector3Value;
            return true;
        }

        private Vector3 GetBaseAvatarViewPosition(
            Component descriptor,
            Vector3 currentViewPosition)
        {
            if (_baseAvatarViewPosition.HasValue)
            {
                return _baseAvatarViewPosition.Value;
            }

            var source = PrefabUtility
                .GetCorrespondingObjectFromSource(descriptor) as Component;
            if (!TryReadAvatarViewPosition(source, out var baseViewPosition) ||
                baseViewPosition.y <= 0.01f)
            {
                baseViewPosition = currentViewPosition;
            }
            _baseAvatarViewPosition = baseViewPosition;
            return baseViewPosition;
        }

        private Animator FindHumanoidAnimator()
        {
            if (_context.Root == null)
            {
                return null;
            }

            return _context.Root
                .GetComponentsInChildren<Animator>(true)
                .FirstOrDefault(animator =>
                    animator != null &&
                    _context.IsInSelectedPrefabScope(animator.transform) &&
                    animator.avatar != null &&
                    animator.avatar.isHuman &&
                    animator.isHuman);
        }

        private IReadOnlyList<Transform> ResolveBodyScaleTargets(
            BodyScaleDefinition definition,
            Animator animator)
        {
            if (definition.UsesAvatarRoot)
            {
                return _context.Root != null &&
                       (!_context.SelectedPrefabSiblingIndex.HasValue ||
                        _context.SelectedPrefabSiblingIndex == -1)
                    ? new[] { _context.Root.transform }
                    : Array.Empty<Transform>();
            }
            if (animator == null)
            {
                return Array.Empty<Transform>();
            }

            var targets = new List<Transform>();
            foreach (var bone in definition.Bones)
            {
                var target = animator.GetBoneTransform(bone);
                if (target == null ||
                    !_context.IsInSelectedPrefabScope(target))
                {
                    continue;
                }

                targets.Add(target);
                if (definition.UsesFirstAvailableBone)
                {
                    break;
                }
            }
            return targets;
        }

        private static Vector3 GetBodyScaleMultipliers(
            IReadOnlyList<Transform> targets)
        {
            var sum = Vector3.zero;
            var count = 0;
            foreach (var target in targets)
            {
                if (target == null)
                {
                    continue;
                }

                var baseScale = GetBaseLocalScale(target);
                sum += new Vector3(
                    GetScaleRatio(target.localScale.x, baseScale.x),
                    GetScaleRatio(target.localScale.y, baseScale.y),
                    GetScaleRatio(target.localScale.z, baseScale.z));
                count++;
            }

            return count == 0
                ? Vector3.one
                : RoundBodyScaleMultipliers(sum / count);
        }

        private static float GetScaleRatio(float value, float baseValue)
        {
            return Mathf.Abs(baseValue) < 0.0001f
                ? 1f
                : value / baseValue;
        }

        private static Vector3 RoundBodyScaleMultipliers(Vector3 value)
        {
            return new Vector3(
                Mathf.Round(value.x * 100f) / 100f,
                Mathf.Round(value.y * 100f) / 100f,
                Mathf.Round(value.z * 100f) / 100f);
        }

        private static float AverageVectorComponent(Vector3 value)
        {
            return (value.x + value.y + value.z) / 3f;
        }

        private static float GetVectorComponent(Vector3 value, int axis)
        {
            return axis == 0 ? value.x : axis == 1 ? value.y : value.z;
        }

        private static Vector3 SetVectorComponent(
            Vector3 value,
            int axis,
            float component)
        {
            if (axis == 0)
            {
                value.x = component;
            }
            else if (axis == 1)
            {
                value.y = component;
            }
            else
            {
                value.z = component;
            }
            return value;
        }

        private static string GetScaleAxisName(int axis)
        {
            return I18N.Get(
                axis == 0
                    ? "workflow.appearance.sizeAxis.x"
                    : axis == 1
                        ? "workflow.appearance.sizeAxis.y"
                        : "workflow.appearance.sizeAxis.z");
        }

        private static Vector3 GetBaseLocalScale(Transform target)
        {
            var source = PrefabUtility
                .GetCorrespondingObjectFromSource(target) as Transform;
            return source != null ? source.localScale : Vector3.one;
        }

        private Vector3 GetCachedBaseLocalScale(
            string path,
            Transform target)
        {
            var key = path ?? string.Empty;
            if (_bodyScaleBaseScales.TryGetValue(key, out var scale))
            {
                return scale;
            }

            scale = GetBaseLocalScale(target);
            _bodyScaleBaseScales[key] = scale;
            return scale;
        }

    }
}
